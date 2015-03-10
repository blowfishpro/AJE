﻿using KSP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace AJE
{


    public class AJEModule : PartModule
    {
        [KSPField(isPersistant = false, guiActive = false)]
        public float Area = 0.1f;
        public float TPR = 1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float BPR = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public float CPR = 20;
        [KSPField(isPersistant = false, guiActive = false)]
        public float FPR = 1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Mdes = 0.9f;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Tdes = 250;
        [KSPField(isPersistant = false, guiActive = false)]
        public float eta_c = 0.95f;
        [KSPField(isPersistant = false, guiActive = false)]
        public float eta_t = 0.98f;
        [KSPField(isPersistant = false, guiActive = false)]
        public float eta_n = 0.9f;
        [KSPField(isPersistant = false, guiActive = false)]
        public float FHV = 46.8E6f;
        [KSPField(isPersistant = false, guiActive = false)]
        public float TIT = 1200;
        [KSPField(isPersistant = false, guiActive = false)]
        public float TAB = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public bool exhaustMixer = false;
        [KSPField(isPersistant = false, guiActive = true)]
        public String Inlet;
        [KSPField(isPersistant = false, guiActiveEditor = true)]
        public float Need_Area;
        [KSPField(isPersistant = false, guiActive = false)]
        public float maxThrust = 999999;
        [KSPField(isPersistant = false, guiActive = false)]
        public float maxT3 = 9999;
        [KSPField(isPersistant = false, guiActive = true)]
        public String Environment;
        [KSPField(isPersistant = true, guiActive = false)]
        public float actualThrottle = 0;

        // Variable bypass stuff
        [KSPField(isPersistant = false, guiActive = false)]
        public bool useVariableBypass = false;
        [KSPField(isPersistant = false, guiActive = false)]
        public int defaultBypassSetting = 100;
        [KSPField(isPersistant = false, guiActive = false)]
        public int designBypassSetting = 100;
        [KSPField(isPersistant = false, guiActive = false)]
        public float bypassResponseRate = 20.0f;
        [KSPField(isPersistant = false, guiActive = false)]
        public FloatCurve defaultBypassCurve = new FloatCurve();

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Bypass Setting", guiFormat = "F0", guiUnits="%")]
        public float commandedBypassSetting = -100.0f;
        [KSPField(isPersistant = true, guiActive = false)]
        public float actualBypassSetting = 100.0f;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool usingBypassCurve = true;

        // Will become a global setting at some point, when global settings are implemented
        float bypassIncrement = 20.0f;

        [KSPEvent(name = "SwitchBypassControlMode", guiActive = true, guiActiveEditor = true, guiName = "Bypass Control: Auto")]
        public void SwitchBypassControlMode()
        {
            if (useVariableBypass && defaultBypassCurve != null)
            {
                if (usingBypassCurve)
                {
                    Events["SwitchBypassControlMode"].guiName = "Bypass Control: Manual";
                    usingBypassCurve = false;
                    commandedBypassSetting = defaultBypassSetting;
                    Events["IncreaseBypass"].guiActive = true;
                    Events["IncreaseBypass"].guiActiveEditor = true;
                    Events["DecreaseBypass"].guiActive = true;
                    Events["DecreaseBypass"].guiActiveEditor = true;
                }
                else
                {
                    Events["SwitchBypassControlMode"].guiName = "Bypass Control: Auto";
                    usingBypassCurve = true;
                    // This is mainly for the editor
                    commandedBypassSetting = defaultBypassCurve.Evaluate((float)aje.GetM0());
                    Events["IncreaseBypass"].guiActive = false;
                    Events["IncreaseBypass"].guiActiveEditor = false;
                    Events["DecreaseBypass"].guiActive = false;
                    Events["DecreaseBypass"].guiActiveEditor = false;
                }
                // Refresh part action window - code from BDAnimationModules
                foreach (UIPartActionWindow window in FindObjectsOfType(typeof(UIPartActionWindow)))
                {
                    if (window.part == part)
                    {
                        window.displayDirty = true;
                    }
                }
            }
        }
        [KSPAction("Switch Bypass Control Mode")]
        public void SwitchBypassControlModeAction(KSPActionParam param)
        {
            SwitchBypassControlMode();
        }
        [KSPEvent(name = "IncreaseBypass", guiActive = true, guiActiveEditor = true, guiName = "Increase Bypass")]
        public void IncreaseBypass()
        {
            if (usingBypassCurve) return;
            if (commandedBypassSetting < 100.0f)
            {
                commandedBypassSetting += bypassIncrement;
            }
            commandedBypassSetting = Mathf.Min(commandedBypassSetting, 100.0f);
        }
        [KSPAction("Increase Bypass")]
        public void IncreaseBypassAction(KSPActionParam param)
        {
            IncreaseBypass();
        }
        [KSPEvent(name = "DecreaseBypass", guiActive = true, guiActiveEditor = true, guiName = "Decrease Bypass")]
        public void DecreaseBypass()
        {
            if (usingBypassCurve) return;
            if (commandedBypassSetting > 0.0f)
            {
                commandedBypassSetting -= bypassIncrement;
            }
            commandedBypassSetting = Mathf.Max(commandedBypassSetting, 0.0f);
        }
        [KSPAction("Decrease Bypass")]
        public void DecreaseBypassAction(KSPActionParam param)
        {
            DecreaseBypass();
        }
        
        public AJESolver aje;
        public EngineWrapper engine;
        public List<AJEModule> engineList;
        public List<AJEInlet> inletList;
        public float OverallTPR = 1, Arearatio = 1;
        public void Start()
        {
            Need_Area = Area * (1 + BPR);
            engine = new EngineWrapper(part);
            engine.idle = 1f;
            engine.IspMultiplier = 1f;
            engine.useVelocityCurve = false;
            engine.ThrustUpperLimit = maxThrust;
 //           bool DREactive = AssemblyLoader.loadedAssemblies.Any(
 //               a => a.assembly.GetName().Name.Equals("DeadlyReentry.dll", StringComparison.InvariantCultureIgnoreCase));
            if (TIT > part.maxTemp)
                part.maxTemp = TIT;
            engine.heatProduction = part.maxTemp * 0.1f;

            float initialArea = Area;
            float initialBPR = BPR;
            if (useVariableBypass)
            {
                float min, max;
                defaultBypassCurve.FindMinMaxValue(out min, out max);
                // if min == max then bypass curve is either absent or bogus
                if (min == max)
                {
                    defaultBypassCurve = null;
                    usingBypassCurve = false;
                    Events["SwitchBypassControlMode"].active = false;
                    Actions["SwitchBypassControlModeAction"].active = false;
                }
                else
                {
                    if (usingBypassCurve)
                    {
                        // Hide these to begin with if using bypass curve
                        Events["IncreaseBypass"].guiActive = false;
                        Events["IncreaseBypass"].guiActiveEditor = false;
                        Events["DecreaseBypass"].guiActive = false;
                        Events["DecreaseBypass"].guiActiveEditor = false;
                    }
                }

                if (commandedBypassSetting < 0)
                    commandedBypassSetting = defaultBypassSetting;
                actualBypassSetting = (float)commandedBypassSetting;

                float bypassMultiplier = 1 + (BPR * (1 - (designBypassSetting / 100.0f)));
                initialArea *= bypassMultiplier;
                initialBPR *= designBypassSetting / 100.0f / bypassMultiplier;
            }
            else
            {
                Fields["commandedBypassSetting"].guiActive = false;
                Fields["commandedBypassSetting"].guiActiveEditor = false;
                Events["IncreaseBypass"].active = false;
                Events["DecreaseBypass"].active = false;
                Events["SwitchBypassControlMode"].active = false;
                Actions["IncreaseBypassAction"].active = false;
                Actions["DecreaseBypassAction"].active = false;
                Actions["SwitchBypassControlModeAction"].active = false;
            }

            aje = new AJESolver();
            aje.InitializeOverallEngineData(
                initialArea,
                TPR,
                initialBPR,
                CPR,
                FPR,
                Mdes,
                Tdes,
                eta_c,
                eta_t,
                eta_n,
                FHV,
                TIT,
                TAB,
                exhaustMixer
                );

            if (CPR != 1)
            {
                engine.engineDecelerationSpeed = .1f / (Area * (1 + BPR));
                engine.engineAccelerationSpeed = .1f / (Area * (1 + BPR));
            }
            else
            {           //It's not like there's anything in a ramjet to spool, now is there?
                engine.useEngineResponseTime = false;
            }
            engineList.Clear();
            inletList.Clear();
            for (int j = 0; j < vessel.parts.Count; j++)        //reduces garbage produced compared to foreach due to Unity Mono issues
            {
                Part p = vessel.parts[j];
                if (p.Modules.Contains("AJEModule"))
                {
                    engineList.Add((AJEModule)p.Modules["AJEModule"]);          //consider storing list of affected AJEModules and AJEInlets, perhaps a single one for each vessel.  Would result in better performance     
                }
                if (p.Modules.Contains("AJEInlet"))
                {
                    inletList.Add((AJEInlet)p.Modules["AJEInlet"]); 
                }
            }
        }
              
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;
            if (engine.type == EngineWrapper.EngineType.NONE || !engine.EngineIgnited)
                return;
            if (vessel.mainBody.atmosphereContainsOxygen == false || part.vessel.altitude > vessel.mainBody.maxAtmosphereAltitude)
            {
                engine.SetThrust(0);
                return;
            }

            UpdateInletEffects();
            if (useVariableBypass)
            {
                // usingBypassCurve _should_ be false if defaultBypassCurve is null, but just to be sure...
                if (usingBypassCurve && defaultBypassCurve != null)
                {
                    commandedBypassSetting = defaultBypassCurve.Evaluate((float)aje.GetM0());
                }
                UpdateBypass();
            }
            UpdateFlightCondition(vessel.altitude, part.vessel.srfSpeed, vessel.mainBody);

            if(CPR == 1 && aje.GetM0()<0.3)//ramjet
            {
                engine.SetThrust(0);
                engine.SetIsp(1000);
            }
            else
            {
                engine.SetThrust((float)aje.GetThrust() / 1000f * Arearatio);
                engine.SetIsp((float)aje.GetIsp());
            }
            float fireflag = (float)aje.GetT3()/maxT3;
            if (fireflag > 0.8f )
            {
                part.temperature = (fireflag * 2f - 1f) * part.maxTemp;
            }
        }

        //ferram4: separate out so function can be called separately for editor sims
        public void UpdateInletEffects()
        {
            float EngineArea = 0, InletArea = 0;
            OverallTPR = 0;

            if (aje == null)
                Debug.Log("HOW?!");
            float M0 = (float)aje.GetM0();
            
            for (int j = 0; j < engineList.Count; j++)       
            {
                AJEModule e = engineList[j];
                if(e)
                {
                    EngineArea += e.Area * (1 + e.BPR);
                }
            }
                        
            for (int j = 0; j < inletList.Count; j++)        
            {
                AJEInlet i = inletList[j];
                if(i)
                {
                    InletArea += i.Area;
                    OverallTPR += i.Area * i.cosine * i.cosine * i.GetTPR(M0);
                }
            }
        
            if (InletArea > 0)
                OverallTPR /= InletArea;
            Arearatio = Math.Min(InletArea / EngineArea, 1f);
            Inlet = "Area:" + Arearatio.ToString("P2") + " TPR:" + OverallTPR.ToString("P2");

        }

        public void UpdateFlightCondition(double altitude, double vel, CelestialBody body)
        {
            double p0 = FlightGlobals.getStaticPressure(altitude, body);
            double t0 = FlightGlobals.getExternalTemperature((float)altitude, body) + 273.15;

            Environment = p0.ToString("N2") + " Atm;" + t0.ToString("N2") + " K ";
            if (CPR != 1)
            {     
                float requiredThrottle = (int)(vessel.ctrlState.mainThrottle * engine.thrustPercentage); //0-100
                float deltaT = (float)TimeWarp.fixedDeltaTime;
                float throttleResponseRate = Mathf.Max(2 / Area / (1 + BPR), 5); //percent per second

                float d = requiredThrottle - actualThrottle;
                if (Mathf.Abs(d) > throttleResponseRate * deltaT)
                    actualThrottle += Mathf.Sign(d) * throttleResponseRate * deltaT;
                else
                    actualThrottle = requiredThrottle;
            }
            else // ramjet
            {
                actualThrottle = (int)(vessel.ctrlState.mainThrottle * engine.thrustPercentage);
            }

            aje.SetTPR(OverallTPR);
            aje.CalculatePerformance(p0 * 101.3, t0, vel, (actualThrottle + 1) / 100);
            
        }

        public void UpdateBypass()
        {
            float deltaT = (float)TimeWarp.fixedDeltaTime;
            float d = commandedBypassSetting - actualBypassSetting;
            if (Mathf.Abs(d) > bypassResponseRate * deltaT)
                actualBypassSetting += Mathf.Sign(d) * bypassResponseRate * deltaT;
            else
                actualBypassSetting = commandedBypassSetting;

            float bypassMultiplier = (1 + (BPR * (1 - (actualBypassSetting/100.0f))));
            float newArea = Area * bypassMultiplier;
            float newBPR = BPR * actualBypassSetting / 100.0f / bypassMultiplier;
            aje.SetAreaAndBypass(newArea, newBPR);
        }
    }
}

