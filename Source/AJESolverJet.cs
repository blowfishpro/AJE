﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP;
using SolverEngines;


namespace AJE
{
    public class AJESolverJet : EngineSolver
    {

        //overall engine design parameters; bypass ratio, fan pressure ratio, 
        //compressor pressure ratio, turbine temperature ratio, 
        private double BPR, FPR, CPR, TTR, inv_BPRp1;

        //engine design point; mach number, temperature 
        private double M_d, T_d;

        //pressure ratios
        private double prat2, prat3;

        //conditions at fan; pressure, temperature;
        private double P2, T2;

        //Conditions at burner inlet / compressor exit
        private double P3, T3, eta_c, inv_eta_c;

        //conditions at burner exit / turbine entrance; pressure, temperature, mass flow rate
        private double P4, T4, eta_t;

        //conditions at ab inlet / turbine exit
        private double P5, T5;

        //conditions at ab exhaust mixer;
        private double P6, T6;

        //conditions at ab rear / nozzle entrance;
        private double P7, T7, eta_n;


        //gas properties: pre-burner in base, post-burner, post afterburner
        private double gamma_t, gamma_ab;
        private double R_t, R_ab;
        private double Cp_t, Cp_ab;
        private double Cv_t, Cv_ab;

        //Air flow, fuel mass fraction for burner and afterburner
        private double mdot, ff, ff_ab;

        //Fuel heat of burning and peak temperatures
        private double h_f, Tt4, Tt7;

        //Reference area of the engine, combustor, and nozzle
        private double Aref, Acomb, Anozzle;
        private double spoolFactor;

        //use exhaust mixer or not
        private bool exhaustMixer;

        //---------------------------------------------------------
        //Initialization Functions

        public void InitializeOverallEngineData(
            double Area,
            double bypassRatio,
            double compressorRatio,
            double fanRatio,
            double designMach,
            double designTemperature,
            double compressorEta,
            double turbineEta,
            double nozzleEta,
            double heatOfFuel,
            double max_TIT,
            double max_TAB,
            bool useExhaustMixer
            )
        {

            Aref = Area;
            spoolFactor = 1d - 0.05 * Math.Min(1d / Aref, 9d);
            BPR = bypassRatio; inv_BPRp1 = 1d / (1d + BPR);
            CPR = compressorRatio;
            FPR = fanRatio;
            M_d = designMach;
            T_d = designTemperature;
            eta_c = compressorEta; inv_eta_c = 1d / eta_c;
            eta_t = turbineEta;
            eta_n = nozzleEta;
            h_f = heatOfFuel;
            Tt4 = max_TIT;
            Tt7 = max_TAB;
            exhaustMixer = useExhaustMixer;

            gamma_c = CalculateGamma(T_d, 0);
            Cp_c = CalculateCp(T_d, 0);
            T1 = T_d * (1 + 0.5 * (gamma_c - 1) * M_d * M_d); //calculate TTR at design point first
            T2 = T1 * Math.Pow(FPR, (gamma_c - 1) / gamma_c / eta_c);
            T3 = T1 * Math.Pow(CPR, (gamma_c - 1) / gamma_c / eta_c);

            ff = Cp_c * (Tt4 - T3) / (Cp_c * (Tt4 - T3) + h_f);//fuel fraction
            gamma_t = CalculateGamma(Tt4, ff);
            Cp_t = CalculateCp(Tt4, ff);


            TTR = 1 - (BPR * Cp_c * (T2 - T1) + Cp_c * (T3 - T1)) / ((1 + ff) * Cp_t * Tt4);

        }

        public override void CalculatePerformance(double airRatio, double commandedThrottle, double flowMult, double ispMult)
        {
            // set base bits
            base.CalculatePerformance(airRatio, commandedThrottle, flowMult, ispMult);
            
            // if we're not combusting, don't combust and start cooling off
            bool shutdown = !running;
            statusString = "Nominal";
            if (running && (!oxygen || eair0 <= 0d))
            {
                shutdown = true;
                statusString = "No oxygen";
            }
            else if(ffFraction <= 0d)
            {
                shutdown = true;
                statusString = "No fuel";
            }
            else if(CPR == 1 && M0 < 0.3d)
            {
                shutdown = true;
                statusString = "Below ignition speed";
            }
            else if(airRatio < 0.01d)
            {
                shutdown = true;
                statusString = "Insufficient intake area";
            }

            if(shutdown)
            {
                double shutdownScalar = Math.Pow(spoolFactor, TimeWarp.fixedDeltaTime);
                
                T3 = Math.Max(t0, T3 * shutdownScalar - 4d);

                mainThrottle = Math.Max(0d, mainThrottle * shutdownScalar - 0.05d);
                if(Tt7 > 0)
                    abThrottle = Math.Max(0d, abThrottle * shutdownScalar - 0.05d);

                return;
            }

            // set throttle
            if (Tt7 == 0)
            {
                mainThrottle = commandedThrottle;
            }
            else
            {
                mainThrottle = Math.Min(commandedThrottle * 1.5d, 1.0);
                abThrottle = Math.Max(commandedThrottle * 3d - 2d, 0);
            }

            prat3 = CPR;
            prat2 = FPR;
            double k = FPR / CPR;
            double p = Math.Pow(k, (gamma_c - 1) * inv_eta_c * inv_gamma_c);
            for (int i = 0; i < 20; i++)    //use iteration to calculate CPR
            {
                P2 = prat2 * P1;
                P3 = prat3 * P1;
                T2 = T1 * Math.Pow(prat2, (gamma_c - 1) * inv_eta_c * inv_gamma_c); //fan
                T3 = T1 * Math.Pow(prat3, (gamma_c - 1) * inv_eta_c * inv_gamma_c); //compressor

                // FIXME use ffFraction here? Instead of just multiplying thrust by fuel fraction in the module?
                // is so, set multiplyThrustByFuelFrac = false in the ModuleEnginesAJEJet.
                T4 = (Tt4 - T3) * mainThrottle + T3;    //burner
                P4 = P3;
                ff = Cp_c * (T4 - T3) / (Cp_c * (T4 - T3) + h_f);//fuel fraction

                Cp_t = CalculateCp(T4, ff);

                T5 = T4 * TTR;      //turbine
                double x = prat3;

                prat3 = (1 + ff) * Cp_t * (T4 - T5) / T1 / Cp_c + 1 + BPR;
                prat3 /= 1 + BPR * p;
                prat3 = Math.Pow(prat3, eta_c * gamma_c * inv_gamma_cm1);
                prat2 = k * prat3;

                if (Math.Abs(x - prat3) < 0.01)
                    break;
            }

            gamma_t = CalculateGamma(T5, ff);//gas parameters
            Cp_t = CalculateCp(T5, ff);
            Cv_t = Cp_t / gamma_t;
            R_t = Cv_t * (gamma_t - 1);

            P5 = P4 * Math.Pow((1 - 1 / eta_t * (1 - TTR)), gamma_t / (gamma_t - 1));

            if (exhaustMixer && BPR > 0)//exhaust mixer
            {
                double Cp6 = (Cp_c * BPR + Cp_t) * inv_BPRp1;//Cp of mixed flow -- kind of
                T6 = T5 * Cp_t / Cp6 * (1 + BPR * Cp_c * T2 / Cp_t / T5) * inv_BPRp1;
                P6 = (P5 + BPR * 0.98 * P2) * inv_BPRp1;
                ff /= (1 + ff + BPR);
                gamma_t = CalculateGamma(T6, ff);//gas parameters
                Cp_t = CalculateCp(T6, ff);
                Cv_t = Cp_t / gamma_t;
                R_t = Cv_t * (gamma_t - 1);

            }
            else
            {
                T6 = T5;
                P6 = P5;
            }


            if (Tt7 > 0)
            {
                T7 = (Tt7 - T6) * abThrottle + T6;//afterburner  
            }
            else
            {
                T7 = T6;
            }

            P7 = P6;//rayleigh loss?

            ff_ab = ff + Cp_t * (T7 - T6) / (Cp_t * (T7 - T6) + h_f);//fuel fraction
            gamma_ab = CalculateGamma(T7, ff_ab);//gas parameters
            Cp_ab = CalculateCp(T7, ff_ab);
            Cv_ab = Cp_ab / gamma_ab;
            R_ab = Cv_ab * (gamma_ab - 1);

            //Nozzle code is from NASA
            double P8 = P7;
            double T8 = T7;

            double p8, V8, A8;
            double epr = P8 / P1;
            double etr = T8 / T1;

            double area8max = .75 * Math.Sqrt(etr) / epr;//ratio of nozzle area to ref area
            A8 = area8max * Aref;
            if (exhaustMixer && BPR > 0)
                A8 *= (1 + BPR);
            double eair = P8 * Math.Sqrt(gamma_ab / R_ab / T8) *
                Math.Pow((.5 + .5 * gamma_ab), .5 * (1 + gamma_ab) / (1 - gamma_ab));//corrected mass flow per area
            mdot = eair * A8;
            double npr = P8 / p0;
            double fac1 = (gamma_ab - 1.0) / gamma_ab;
            V8 = Math.Sqrt(2.0 * R_c / fac1 * T8 * eta_n * (1.0 - Math.Pow(1.0 / npr, fac1)));     //exit velocity
            p8 = (npr <= 1.893) ? p0 : .52828 * P8;
            thrust = V8 * mdot + (p8 - p0) * A8;

            if (BPR > 0 && FPR > 1 && exhaustMixer == false)
            {
                fac1 = (gamma_c - 1) * inv_gamma_c; //fan thrust from NASA
                double snpr = P2 / p0;
                double ues = Math.Sqrt(2.0 * R_c / fac1 * T2 * eta_n * (1.0 - Math.Pow(1.0 / snpr, fac1)));
                double pfexit = (snpr <= 1.893) ? p0 : .52828 * P2; //exit pressure of fan 
                thrust += BPR * ues * mdot / (1 + ff_ab) + (pfexit - p0) * BPR * Aref;
            }


            thrust -= mdot / (1 + ff_ab) * (1 + (exhaustMixer ? 0 : BPR)) * (vel);//ram drag

            thrust *= flowMult * ispMult;
            fuelFlow = mdot * ff_ab * flowMult;
            Isp = thrust / (fuelFlow * 9.80665);
            thrust *= airRatio; // FIXME: should this get applied to fuel flow and Isp too?

            /*  
              debugstring = "";
              debugstring += "TTR:\t" + TTR.ToString("F3") + "\r\n";
              debugstring += "CPR:\t" + prat3.ToString("F3") + "\r\n"; ;
              debugstring += "p0: " + p0.ToString("F2") + "\tt0: " + t0.ToString("F2") + "\r\n";
              debugstring += "P1: " + P1.ToString("F2") + "\tT1: " + T1.ToString("F2") + "\r\n";
              debugstring += "P2: " + P2.ToString("F2") + "\tT2: " + T2.ToString("F2") + "\r\n";
              debugstring += "P3: " + P3.ToString("F2") + "\tT3: " + T3.ToString("F2") + "\r\n";
              debugstring += "P4: " + P4.ToString("F2") + "\tT4: " + T4.ToString("F2") + "\r\n";
              debugstring += "P5: " + P5.ToString("F2") + "\tT5: " + T5.ToString("F2") + "\r\n";
              debugstring += "P6: " + P6.ToString("F2") + "\tT6: " + T6.ToString("F2") + "\r\n";
              debugstring += "P7: " + P7.ToString("F2") + "\tT7: " + T7.ToString("F2") + "\r\n";
              debugstring += "EPR: " + epr.ToString("F2") + "\tETR: " + etr.ToString("F2") + "\r\n";

              debugstring += "FF: " + ff.ToString("P") + "\t";
              debugstring += "FF_AB: " + ff_ab.ToString("P") + "\r\n";
              debugstring += "V8: " + V8.ToString("F2") + "\tA8: " + A8.ToString("F2") + "\r\n";
              debugstring += "Thrust: " + (thrust / 1000).ToString("F1") + "\tmdot: " + mdot.ToString("F2") + "\r\n";
              debugstring += "NetThrust: " + (thrust / 1000).ToString("F1") + "\tSFC: " + (3600 / Isp).ToString("F3") + "\r\n";
              Debug.Log(debugstring);*/
        }

        public override double GetEngineTemp() { return T3; }
        public override double GetArea() { return Aref * (1d + BPR); }
        public override double GetEmissive()
        {
            if (Tt7 == 0)
                return mainThrottle;
            else
                return (mainThrottle * 0.25d + abThrottle * 0.75d);
        }

        public double Prat3 { get { return prat3; } }
    }

}
