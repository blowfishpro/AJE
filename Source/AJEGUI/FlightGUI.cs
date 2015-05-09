using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace AJE.AJEGUI
{
    public class FlightGUI : VesselModule
    {
        private Vessel vessel;
        private AJEFlightSys flightSys;
        private bool inAtmosphere;

        // Toolbar stuff

        private static IButton AJEFlightButtonBlizzy = null;
        private static ApplicationLauncherButton AJEFlightButtonStock = null;

        // GUI stuff

        public static bool HideUIFlight = false;
        public static bool MinimizeUIFlight = false;

        public static Rect FlightWindowPos;

        private static int windowDisplayField = 0; // Bitfield describing what is visible in the flight window - used to determine when the height needs to be updated

        private void Awake()
        {
            FlightDataWrapper.Init();
        }

        private void Start()
        {
            vessel = gameObject.GetComponent<Vessel>();
            flightSys = gameObject.GetComponent<AJEFlightSys>();
            this.enabled = true;
            FlightGUISettings.ShowSettingsWindow = false;
            GUIUnitsSettings.ShowUnitsSettingsWindow = false;

            if (vessel.isActiveVessel)
            {
                FlightGUISettings.LoadSettingsFromConfig();
            }

            if (ToolbarManager.ToolbarAvailable && AJEFlightButtonBlizzy == null)
            {
                AJEFlightButtonBlizzy = ToolbarManager.Instance.add("AJE", "AJEFlightButton");
                AJEFlightButtonBlizzy.TexturePath = "AJE/Icons/AJEIconBlizzy";
                AJEFlightButtonBlizzy.ToolTip = "Advanced Jet Engine";
                AJEFlightButtonBlizzy.OnClick += (e) => MinimizeUIFlight = !MinimizeUIFlight;
            }
            else
                GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);

            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onHideUI.Add(HideUI);
        }

        private void OnDestroy()
        {
            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onHideUI.Remove(HideUI);
            FlightGUISettings.SaveConfigs();

            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            if (AJEFlightButtonStock != null)
                ApplicationLauncher.Instance.RemoveModApplication(AJEFlightButtonStock);

            if (AJEFlightButtonBlizzy != null)
                AJEFlightButtonBlizzy.Destroy();
        }

        #region GUI

        public void OnGUI()
        {
            if (!vessel.isActiveVessel || MinimizeUIFlight || HideUIFlight)
                return;

            if (!GUIUtil.StylesInitialized)
                GUIUtil.SetupStyles();

            FlightWindowPos = GUILayout.Window(GUIUtil.FlightWindowID, FlightWindowPos, FlightWindowGUI, "Advanced Jet Engine", GUILayout.MinWidth(150));

            FlightGUISettings.OnSettingsWindowGUI();
            GUIUnitsSettings.OnUnitsSettingsWindowGUI();
        }

        public void FlightWindowGUI(int windowID)
        {
            inAtmosphere = vessel.altitude < vessel.mainBody.atmosphereDepth;
            int tmpDisplayField = windowDisplayField;
            windowDisplayField = 0;
            int counter = 1;

            GUILayout.BeginVertical();

            if (FlightGUISettings.ShowAmbientTemp)
            {
                GUIUtil.FlightWindowField("Ambient Temperature", GUIUnitsSettings.TemperatureUnits.Format(vessel.atmosphericTemperature, GUIUnits.Temperature.kelvin));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowAmbientPressure)
            {
                GUIUtil.FlightWindowField("Ambient Pressure", GUIUnitsSettings.PressureUnits.Format(vessel.staticPressurekPa, GUIUnits.Pressure.kPa));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowRecoveryTemp && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("Recovery Temperature", GUIUnitsSettings.TemperatureUnits.Format(flightSys.InletTherm.T, GUIUnits.Temperature.kelvin));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowRecoveryPressure && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("Recovery Pressure", GUIUnitsSettings.PressureUnits.Format(flightSys.InletTherm.P, GUIUnits.Pressure.Pa));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowInletPercent && inAtmosphere && flightSys.EngineArea > 0f)
            {
                GUIStyle inletPercentStyle = new GUIStyle(GUIUtil.LeftLabel);
                float.IsInfinity(flightSys.AreaRatio);

                string areaPercentString = "";
                if (float.IsInfinity(flightSys.AreaRatio) || float.IsNaN(flightSys.AreaRatio))
                    areaPercentString = "n/a";
                else
                {
                    areaPercentString = flightSys.AreaRatio.ToString("P2");
                    Color inletPercentColor = Color.white;
                    if (flightSys.AreaRatio >= 1)
                        inletPercentColor = Color.green;
                    else if (flightSys.AreaRatio < 1)
                        inletPercentColor = Color.red;
                    inletPercentStyle.normal.textColor = inletPercentStyle.focused.textColor = inletPercentStyle.hover.textColor = inletPercentStyle.onNormal.textColor = inletPercentStyle.onFocused.textColor = inletPercentStyle.onHover.textColor = inletPercentStyle.onActive.textColor = inletPercentColor;
                }

                GUIUtil.FlightWindowField("Inlet", areaPercentString);
                windowDisplayField += counter;
            }
            counter *= 2;

            if (FlightGUISettings.ShowTPR && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("TPR", flightSys.OverallTPR.ToString("P2"));
                windowDisplayField += counter;
            }
            counter *= 2;

            if (FlightGUISettings.ShowInletPressureRatio && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("Inlet Pressure Ratio", (flightSys.InletTherm.P / flightSys.AmbientTherm.P).ToString("F2"));
                windowDisplayField += counter;
            }
            counter *= 2;

            double totalThrust = 0;
            double totalMDot = 0;

            if (FlightGUISettings.ShowThrust || FlightGUISettings.ShowTWR || FlightGUISettings.ShowTDR || FlightGUISettings.ShowIsp || FlightGUISettings.ShowTSFC)
            {
                List<ModuleEngines> allEngines = flightSys.EngineList;
                for (int i = 0; i < allEngines.Count; i++)
                {
                    if (allEngines[i].EngineIgnited)
                    {
                        totalThrust += allEngines[i].finalThrust; // kN
                        totalMDot += allEngines[i].finalThrust / allEngines[i].realIsp;
                    }
                }
                totalMDot /= 9.81d;
                totalMDot *= 1000d; // kg/s
            }

            if (FlightGUISettings.ShowThrust)
            {
                GUIUtil.FlightWindowField("Thrust", GUIUnitsSettings.ForceUnits.Format(totalThrust, GUIUnits.Force.kN));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowTWR)
            {
                double TWR = totalThrust / (FlightGlobals.getGeeForceAtPosition(vessel.CoM).magnitude * vessel.GetTotalMass());

                GUIUtil.FlightWindowField("TWR", TWR.ToString("F2"));
                windowDisplayField += counter;
            }
            counter *= 2;
            
            if (FlightGUISettings.ShowTDR && inAtmosphere)
            {
                double totalDrag = FlightDataWrapper.VesselTotalDragN(vessel);
                double tdr = 0;
                if (FlightDataWrapper.VesselDynPresPa(vessel) > 0.01d)
                    tdr = totalThrust / totalDrag * 1000d;

                GUIUtil.FlightWindowField("Thrust / Drag", tdr.ToString("G3"));
                windowDisplayField += counter;
            }
            counter *= 2;

            if (FlightGUISettings.ShowIsp)
            {
                double Isp = 0d;
                if (totalThrust > 0d && totalMDot > 0d)
                    Isp = totalThrust / totalMDot; // kN/(kg/s) = km/s
                Debug.Log("Isp = " + Isp.ToString("G3"));

                GUIUtil.FlightWindowField("Specific Impulse", GUIUnitsSettings.IspUnits.Format(Isp, GUIUnits.Isp.km__s));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowTSFC)
            {
                double SFC = 0d;
                if (totalThrust > 0d && totalMDot > 0d)
                    SFC = totalMDot / totalThrust; // (kg/s)/kN

                GUIUtil.FlightWindowField("TSFC", GUIUnitsSettings.TSFCUnits.Format(SFC, GUIUnits.TSFC.kg__kN_s));
                windowDisplayField += counter;
            }

            if (windowDisplayField != tmpDisplayField)
                FlightWindowPos.height = 0;

            GUILayout.BeginHorizontal();
            FlightGUISettings.ShowSettingsWindow = GUILayout.Toggle(FlightGUISettings.ShowSettingsWindow, "Settings", GUIUtil.ButtonToggle, GUIUtil.normalWidth);
            GUIUnitsSettings.ShowUnitsSettingsWindow = GUILayout.Toggle(GUIUnitsSettings.ShowUnitsSettingsWindow, "Units", GUIUtil.ButtonToggle, GUIUtil.smallWidth);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();

            FlightWindowPos = GUIUtil.ClampToScreen(FlightWindowPos);
        }

        #endregion

        #region Toolbar Methods

        private static void HideUI()
        {
            HideUIFlight = true;
        }

        private static void ShowUI()
        {
            HideUIFlight = false;
        }

        private static void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && AJEFlightButtonStock == null)
            {
                AJEFlightButtonStock = ApplicationLauncher.Instance.AddModApplication(
                    onAppLaunchToggleOn,
                    onAppLaunchToggleOff,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    (Texture)GameDatabase.Instance.GetTexture("AJE/Icons/AJEIconStock", false));
            }
        }

        private static void DummyVoid() { }

        private static void onAppLaunchToggleOn()
        {
            MinimizeUIFlight = false;
        }

        private static void onAppLaunchToggleOff()
        {
            MinimizeUIFlight = true;
        }

        #endregion
    }
}
