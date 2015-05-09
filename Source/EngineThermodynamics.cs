using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AJE
{
    // Right now this only works for a mixture of mostly air and some fuel (presumably kerosene-based)
    // To work well for other engine types, it should be changed to be composed of an arbitrary mixture of exhaust products, and may or may not have air
    // The exhaust products would presumably have their thermodynamic parameters read from a cfg somewhere, including possible temperature dependence
    public class EngineThermodynamics
    {
        private double _T; // Total temperature
        private double _Far;
        private double _FF;

        public double P { get; set; } // Total pressure
        public double T
        {
            get
            {
                return _T;
            }
            set
            {
                _T = value;
                Recalculate();
            }
        }
        public double Cp { get; private set; }
        public double Cv { get; private set; }
        public double Gamma { get; private set; }
        public double R { get; private set; }
        public double Far
        {
            get
            {
                return _Far;
            }
            set
            {
                _Far = value;
                _FF = _Far / (_Far + 1);
                Recalculate();
            }
        }
        public double FF
        {
            get
            {
                return _FF;
            }
            set
            {
                _FF = value;
                _Far = _FF / (_FF - 1);
                Recalculate();
            }
        }

        public EngineThermodynamics()
        {
            P = 0;
            _T = 0;
            _Far = 0;
            _FF = 0;
            Cp = 0;
            Cv = 0;
            Gamma = 0;
            R = 0;
        }

        public void CopyFrom(EngineThermodynamics t)
        {
            P = t.P;
            _T = t.T;
            _Far = t.Far;
            _FF = t.FF;
            Cp = t.Cp;
            Cv = t.Cv;
            Gamma =t.Gamma;
            R = t.R;
        }

        private void Recalculate()
        {
            double tDelt = Math.Max((T - 300) * 0.0005, 0);
            Cp = 1004.5 + 250 * tDelt * (1 + 10 * FF);
            Gamma = 1.4 - 0.1 * tDelt * (1 + FF);
            Cv = Cp / Gamma;
            R = Cv * (Gamma - 1);
        }

        // This odd system of copying from another Thermodynamics and then modifying is so that new ones don't have to be allocated every frame, which is bad for GC

        public void FromAdiabaticProcessWithTempRatio(EngineThermodynamics t, double tempRatio, double efficiency = 1)
        {
            CopyFrom(t);
            _T *= tempRatio;
            P *= Math.Pow(tempRatio, Gamma / (Gamma - 1) * efficiency);
            Recalculate();
        }

        public void FromAdiabaticProcessWithPressureRatio(EngineThermodynamics t, double pressureRatio, double efficiency = 1)
        {
            CopyFrom(t);
            _T *= Math.Pow(pressureRatio, (Gamma - 1) / Gamma / efficiency);
            P *= pressureRatio;
            Recalculate();
        }

        public void FromAdiabaticProcessWithWork(EngineThermodynamics t, double work, double efficiency = 1)
        {
            CopyFrom(t);
            _T += work / Cp;
            P *= Math.Pow(T / t.T, t.Gamma / (t.Gamma - 1) * efficiency);
            Recalculate();
        }

        public void FromChangeReferenceFrame(EngineThermodynamics t, double speed, double efficiency = 1, bool forward=true)
        {
            FromAdiabaticProcessWithWork(t, speed * speed / 2d * (forward? 1d : -1d), efficiency);
        }

        public void FromAddFuelToTemperature(EngineThermodynamics t, double maxTemp, double heatOfFuel, float throttle = 1f, float maxFar = 0f)
        {
            CopyFrom(t);
            double delta = (maxTemp - T) * throttle;

            // Max fuel-air ratio - don't want to inject more fuel than can be burnt in air
            if (maxFar > 0)
            {
                if (Far >= maxFar)
                    delta = 0;
                else
                    delta = Math.Max(delta, (maxFar - Far) * heatOfFuel / Cp);
            }
            _T += delta;
            Far += delta * Cp / heatOfFuel;
            Recalculate();
        }
    }
}
