using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace AJE
{
    public class AJEFlightSys : VesselModule
    {
        private Vessel vessel;

        public float InletArea { get; private set; }
        public float EngineArea { get; private set; }
        public float AreaRatio { get; private set; }
        public double OverallTPR { get; private set; }
        public List<ModuleEngines> EngineList { get { return allEngines; } }
        
        private int partsCount = 0;
        private List<ModuleEnginesAJEJet> engineList = new List<ModuleEnginesAJEJet>();
        private List<AJEInlet> inletList = new List<AJEInlet>();
        private List<ModuleEngines> allEngines = new List<ModuleEngines>();

        // Ambient conditions - real
        public EngineThermodynamics AmbientTherm;
        public double Mach { get; private set; }

        // Inlet conditions - stagnation

        public EngineThermodynamics InletTherm;

        private bool inAtmosphere = true; // Keeps track of when in atmosphere and oxygen pesent.

        private void Start()
        {
            vessel = gameObject.GetComponent<Vessel>();
            this.enabled = true;
            updatePartsList();

            AmbientTherm = new EngineThermodynamics();
            InletTherm = new EngineThermodynamics();
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel)
                return;
            if (vessel.altitude > vessel.mainBody.atmosphereDepth)
                return;

            if (partsCount != vessel.Parts.Count)
                updatePartsList();

            AmbientTherm.P = vessel.staticPressurekPa * 1000d; // Pa
            AmbientTherm.T = vessel.atmosphericTemperature;
            Mach = vessel.mach;

            InletArea = 0;
            EngineArea = 0;
            OverallTPR = 0;

            for (int j = 0; j < engineList.Count; j++)
            {
                ModuleEnginesAJEJet e = engineList[j];
                if (e && e.EngineIgnited)
                {
                    EngineArea += e.TotalArea;
                }
            }

            for (int j = 0; j < inletList.Count; j++)
            {
                AJEInlet i = inletList[j];
                if (i && i.intakeEnabled)
                {
                    InletArea += i.Area;
                    //overallTPR += i.Area * i.overallTPR; // when changes from SolverEngines merged
                    OverallTPR += i.Area * i.cosine * i.cosine * i.GetTPR(Mach);
                }
            }

            AreaRatio = InletArea / EngineArea;

            if (InletArea > 0 && EngineArea > 0)
                OverallTPR /= InletArea;
            else
                OverallTPR = 0;

            // Transform from static frame to vessel frame, increasing total pressure and temperature
            InletTherm.FromChangeReferenceFrame(AmbientTherm, vessel.srfSpeed);
            InletTherm.P *= OverallTPR;
        }

        private void updatePartsList()
        {
            engineList.Clear();
            inletList.Clear();
            allEngines.Clear();
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];
                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];
                    if (m is ModuleEngines)
                        allEngines.Add(m as ModuleEngines);
                        if (m is ModuleEnginesAJEJet)
                            engineList.Add(m as ModuleEnginesAJEJet);
                    else if (m is AJEInlet)
                        inletList.Add(m as AJEInlet);
                }
            }
        }
    }
}
