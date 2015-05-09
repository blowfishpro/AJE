using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace AJE.AJEGUI
{
    public static class GUIUnitsSettings
    {
        private static Rect UnitsSettingsWindowPos;
        private static KSP.IO.PluginConfiguration config;

        public static GUIUnits.Units<GUIUnits.Temperature> TemperatureUnits = GUIUnits.Temperature.kelvin;
        public static GUIUnits.Units<GUIUnits.Pressure> PressureUnits = GUIUnits.Pressure.kPa;
        public static GUIUnits.Units<GUIUnits.Force> ForceUnits = GUIUnits.Force.kN;
        public static GUIUnits.Units<GUIUnits.Isp> IspUnits = GUIUnits.Isp.s;
        public static GUIUnits.Units<GUIUnits.TSFC> TSFCUnits = GUIUnits.TSFC.kg__kgf_h;

        public static bool ShowUnitsSettingsWindow = false;

        #region GUI

        public static void OnUnitsSettingsWindowGUI()
        {
            if (ShowUnitsSettingsWindow)
            {
                UnitsSettingsWindowPos = GUILayout.Window(GUIUtil.UnitsSettingsWindowID, UnitsSettingsWindowPos, UnitsSettingsWindowGUI, "AJE Units Settings", GUILayout.MinWidth(150));
            }
        }

        public static void UnitsSettingsWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUIUtil.UnitSelectionGrid<GUIUnits.Temperature>(ref TemperatureUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.Pressure>(ref PressureUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.Force>(ref ForceUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.Isp>(ref IspUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.TSFC>(ref TSFCUnits);

            GUILayout.EndVertical();

            GUI.DragWindow();

            UnitsSettingsWindowPos = GUIUtil.ClampToScreen(UnitsSettingsWindowPos);
        }

        #endregion

        #region Configs

        public static void LoadSettingsFromConfig()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FlightGUI>();
            config.load();

            UnitsSettingsWindowPos = config.GetValue("settingsWindowPos", new Rect(400, 100, 200, 100));
            FlightGUI.MinimizeUIFlight = config.GetValue("minimizeGUI", false);

            PressureUnits = GUIUnits.UnitsFromConfig<GUIUnits.Pressure>(ref config, GUIUnits.Pressure.kPa);
            TemperatureUnits = GUIUnits.UnitsFromConfig<GUIUnits.Temperature>(ref config, GUIUnits.Temperature.kelvin);
            ForceUnits = GUIUnits.UnitsFromConfig<GUIUnits.Force>(ref config, GUIUnits.Force.kN);
            IspUnits = GUIUnits.UnitsFromConfig<GUIUnits.Isp>(ref config, GUIUnits.Isp.s);
            TSFCUnits = GUIUnits.UnitsFromConfig<GUIUnits.TSFC>(ref config, GUIUnits.TSFC.kg__kgf_h);
        }

        public static void SaveConfigs()
        {
            PressureUnits.SaveToConfig(ref config);
            TemperatureUnits.SaveToConfig(ref config);
            ForceUnits.SaveToConfig(ref config);
            IspUnits.SaveToConfig(ref config);
            TSFCUnits.SaveToConfig(ref config);

            config.save();
        }

        #endregion
    }
}
