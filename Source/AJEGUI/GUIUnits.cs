using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using KSP;

namespace AJE.AJEGUI
{
    public static class GUIUnits
    {
        private static List<UnitTypeContainer> unitTypes = new List<UnitTypeContainer>();

        public static bool UnitTypeRegistered<T>()
        {
            foreach (UnitTypeContainer u in unitTypes)
            {
                if (u is UnitTypeContainer<T>)
                    return true;
            }
            return false;
        }

        private static UnitTypeContainer<T> UnitType<T>()
        {
            foreach (UnitTypeContainer u in unitTypes)
            {
                if (u is UnitTypeContainer<T>)
                    return u as UnitTypeContainer<T>;
            }
            throw new ArgumentException("The unit type " + typeof(T).ToString() + " has not been registered.");
        }

        public static int UnitsCount<T>()
        {
            return UnitType<T>().UnitsCount;
        }

        public static bool UnitsRegistered<T>(Units<T> units)
        {
            return UnitType<T>().UnitsRegistered(units);
        }

        public static void RegisterUnitType<T>(string name, string configName)
        {
            if (UnitTypeRegistered<T>())
            {
                Debug.LogWarning("Unit type " + typeof(T).ToString() + " already registered.");
                return;
            }

            unitTypes.Add(new UnitTypeContainer<T>(name, configName));
        }

        public static string[] UnitNames<T>()
        {
            return UnitType<T>().UnitNames;
        }

        public static void RegisterUnits<T>(Units<T> units)
        {
            UnitTypeContainer<T> unitType = UnitType<T>();
            if (unitType == null)
                Debug.LogWarning("Unit type " + typeof(T).ToString() + " has not been registered.  Units " + units.ToString() + " will not be added.");

            unitType.AddUnits(units);
        }

        public static Units<T> UnitsFromConfig<T>(ref KSP.IO.PluginConfiguration config, Units<T> defaultUnits = null)
        {
            UnitTypeContainer<T> unitType = UnitType<T>();
            if (unitType == null)
                throw new ArgumentException("Unit type " + typeof(T).ToString() + " has not been registered.  Cannot load from config.");
            return unitType.UnitsFromConfig(ref config, defaultUnits);
        }

        public static Units<T> UnitsAtIndex<T>(int index)
        {
            return UnitType<T>().UnitsAtIndex(index);
        }

        public static int IndexOfUnits<T>(Units<T> units)
        {
            return UnitType<T>().IndexOfUnits(units);
        }

        public sealed class Pressure
        {
            public static readonly Units<Pressure> Pa = new Units<Pressure>("Pa", format: "F3");
            public static readonly Units<Pressure> kPa = new Units<Pressure>("kPa", multiplier: 0.001f, format: "F2");
            public static readonly Units<Pressure> atm = new Units<Pressure>("atm", multiplier: 9.8692e-6f, format: "G3");

            private Pressure() {} // Cannot be instantiated but needs to be used as generic type

            static Pressure()
            {
                RegisterUnitType<Pressure>("Pressure", "pressureUnits");
                RegisterUnits<Pressure>(Pa);
                RegisterUnits<Pressure>(kPa);
                RegisterUnits<Pressure>(atm);
            }
        }

        public sealed class Temperature
        {
            public static readonly Units<Temperature> kelvin = new Units<Temperature>("kelvin", shortName: "K", format: "F1");
            public static readonly Units<Temperature> celsius = new Units<Temperature>("celsius", shortName: "C", affine: -273.15f, format: "F1");

            private Temperature() { } // Cannot be instantiated but needs to be used as generic type

            static Temperature()
            {
                RegisterUnitType<Temperature>("Temperature", "temperatureUnits");
                RegisterUnits<Temperature>(kelvin);
                RegisterUnits<Temperature>(celsius);
            }
        }

        public sealed class Force
        {
            public static readonly Units<Force> N = new Units<Force>("newtons", shortName: "N", format: "F1");
            public static readonly Units<Force> kN = new Units<Force>("kN", multiplier: 0.001f, format: "F1");

            private Force() { } // Cannot be instantiated but needs to be used as generic type

            static Force()
            {
                RegisterUnitType<Force>("Force", "forceUnits");
                RegisterUnits<Force>(N);
                RegisterUnits<Force>(kN);
            }
        }

        public sealed class Isp
        {
            public static readonly Units<Isp> m__s = new Units<Isp>("m/s", format: "G3");
            public static readonly Units<Isp> km__s = new Units<Isp>("km/s", multiplier: 0.001f, format: "F3");
            public static readonly Units<Isp> s = new Units<Isp>("s", multiplier: 1f / 9.81f, format: "F1");

            private Isp() { } // Cannot be instantiated but needs to be used as generic type

            static Isp()
            {
                RegisterUnitType<Isp>("Isp", "ispUnits");
                RegisterUnits<Isp>(m__s);
                RegisterUnits<Isp>(km__s);
                RegisterUnits<Isp>(s);
            }
        }

        public sealed class TSFC
        {
            public static readonly Units<TSFC> s__m = new Units<TSFC>("s/m", format: "G3");
            public static readonly Units<TSFC> __s = new Units<TSFC>("s^-1", multiplier: 9.81f, format: "G3");
            public static readonly Units<TSFC> kg__kgf_h = new Units<TSFC>("kg/kgf-h", multiplier: 9.81f * 3600f, format: "F2");
            public static readonly Units<TSFC> g__kN_s = new Units<TSFC>("g/kN-s", multiplier: 1e6f, format: "F1");
            public static readonly Units<TSFC> kg__kN_s = new Units<TSFC>("kg/kN-s", multiplier: 1e3f, format: "F3");

            private TSFC() { } // Cannot be instantiated but needs to be used as generic type

            static TSFC()
            {
                RegisterUnitType<TSFC>("TSFC", "tsfcUnits");
                RegisterUnits<TSFC>(s__m);
                RegisterUnits<TSFC>(__s);
                RegisterUnits<TSFC>(kg__kgf_h);
                RegisterUnits<TSFC>(g__kN_s);
                RegisterUnits<TSFC>(kg__kN_s);
            }
        }

        public class Units<T>
        {
            private string name;
            private string shortName;
            private string format = null;

            // Actual transformation

            private float multiplier = 1f;
            private float affine = 0f;

            public string Name { get { return name; } }
            public string ShortName { get { return shortName; } }
            public string ConfigName { get { return name; } }
            public string UnitTypeName { get { return UnitType.Name; } }
            public int Index { get { return UnitType.IndexOfUnits(this); } }

            private UnitTypeContainer<T> UnitType { get { return UnitType<T>(); } } // This will throw an exception if T is not a registed unit type.

            public Units (string name, string shortName = null, float multiplier = 1f, float affine = 0f, string format = null)
            {
                this.name = name;
                if (shortName != null)
                    this.shortName = shortName;
                else
                    this.shortName = name;
                this.multiplier = multiplier;
                this.affine = affine;
                this.format = format;
            }

            public double Convert(double value)
            {
                return value * multiplier + affine;
            }
            public double InverseConvert(double value)
            {
                return (value - affine) / multiplier;
            }
            public string Format(double value)
            {
                return value.ToString(format) + " " + shortName;
            }
            public string Format(double value, Units<T> inUnits = null)
            {
                if (this != inUnits)
                {
                    Units<T> baseUnits = UnitType.BaseUnits;
                    if (inUnits != null && inUnits != baseUnits)
                        value = inUnits.InverseConvert(value);
                    if (this != baseUnits)
                        value = Convert(value);
                }
                return Format(value);
            }

            public void SaveToConfig(ref KSP.IO.PluginConfiguration config)
            {
                config.SetValue(UnitType.ConfigName, ConfigName);
            }

            public override string ToString()
            {
                return name;
            }
        }

        private abstract class UnitTypeContainer { } // Hack to deal with nonspecific generics

        private class UnitTypeContainer<T> : UnitTypeContainer
        {
            private List<Units<T>> unitList = new List<Units<T>>();
            private string[] unitNameStrings = {}; // keep track so it doesn't have to be created every frame in settings gui

            public Units<T> BaseUnits { get; private set; }
            public string Name { get; private set; }
            public string ConfigName { get; private set; }
            public int UnitsCount { get { return unitList.Count; } }

            public UnitTypeContainer(string name, string configName)
            {
                Name = name;
                ConfigName = configName;
                BaseUnits = null;
            }

            public void AddUnits(Units<T> units)
            {
                if (unitList.Contains(units))
                {
                    Debug.LogWarning("Units " + units.ToString() + " already contained in " + ToString());
                    return;
                }

                unitList.Add(units);

                if (BaseUnits == null)
                    BaseUnits = units;

                unitNameStrings = new string[UnitsCount];
                for (int i = 0; i < UnitsCount; i++)
                {
                    unitNameStrings[i] = unitList[i].Name;
                }
            }

            public bool UnitsRegistered(Units<T> units)
            {
                return (unitList.Contains(units));
            }

            public Units<T> UnitsFromConfig(ref KSP.IO.PluginConfiguration config, Units<T> defaultUnits = null)
            {
                if (defaultUnits == null)
                    defaultUnits = BaseUnits;
                else
                    if (!UnitsRegistered(defaultUnits))
                    {
                        Debug.LogWarning("The units \"" + defaultUnits.ToString() + "\" do not exist in" + ToString());
                        defaultUnits = BaseUnits;
                    }

                string name = config.GetValue(ConfigName, defaultUnits.ConfigName);
                if (name == defaultUnits.ConfigName)
                    return defaultUnits;
                foreach (Units<T> units in unitList)
                {
                    if (units.ConfigName == name)
                        return units;
                }
                Debug.LogWarning("The units \"" + name + "\" requested in config do not exist in " + ToString());
                return BaseUnits;
            }

            public Units<T> UnitsAtIndex(int index)
            {
                if (index > UnitsCount)
                    throw new IndexOutOfRangeException("Index " + index.ToString() + " is out of range for " + ToString() + " (there are only " + UnitsCount.ToString() + " unit types).");
                return unitList[index];
            }

            public int IndexOfUnits(Units<T> units)
            {
                for (int i = 0; i < UnitsCount; i++)
                {
                    if (unitList[i] == units)
                        return i;
                }
                throw new ArgumentException("The units \"" + units.ToString() + "\" do not exist in" + ToString());
            }

            public string[] UnitNames
            {
                get
                {
                    string[] outNames = new string[UnitsCount];
                    for (int i = 0; i < UnitsCount; i++)
                        outNames[i] = unitList[i].Name;
                    return outNames;
                }
            }

            public override string ToString()
            {
                return "UnitType<" + typeof(T).ToString() + ">";
            }
        }
    }
}
