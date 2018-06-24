﻿// -----------------------------------------------------------------------
// <copyright file="Series.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace Models.Graph
{
    using System;
    using System.Drawing;
    using System.Xml.Serialization;
    using System.Data;
    using System.Collections.Generic;
    using System.Linq;
    using APSIM.Shared.Utilities;
    using System.Collections;
    using Models.Core;
    using Models.Factorial;
    using Storage;

    /// <summary>The class represents a single series on a graph</summary>
    [ValidParent(ParentType = typeof(Graph))]
    [ViewName("UserInterface.Views.SeriesView")]
    [PresenterName("UserInterface.Presenters.SeriesPresenter")]
    [Serializable]
    public class Series : Model, IGraphable
    {
        /// <summary>Constructor for a series</summary>
        public Series()
        {
            this.Checkpoint = "Current";
            this.XAxis = Axis.AxisType.Bottom;
            this.FactorIndexToVaryColours = -1;
            this.FactorIndexToVaryLines = -1;
            this.FactorIndexToVaryMarkers = -1;
        }

        /// <summary>Gets or sets the series type</summary>
        public SeriesType Type { get; set; }

        /// <summary>Gets or sets the associated x axis</summary>
        public Axis.AxisType XAxis { get; set; }

        /// <summary>Gets or sets the associated y axis</summary>
        public Axis.AxisType YAxis { get; set; }

        /// <summary>
        /// Gets or sets the color represented as a red, green, blue integer
        /// </summary>
        public int ColourArgb { get; set; }

        /// <summary>Gets or sets the color</summary>
        [XmlIgnore]
        public Color Colour
        {
            get
            {
                return Color.FromArgb(this.ColourArgb);
            }

            set
            {
                this.ColourArgb = value.ToArgb();
            }
        }

        /// <summary>The FactorIndex to vary colours.</summary>
        public int FactorIndexToVaryColours { get; set; }

        /// <summary>The FactorIndex to vary markers types.</summary>
        public int FactorIndexToVaryMarkers { get; set; }

        /// <summary>The FactorIndex to vary line types.</summary>
        public int FactorIndexToVaryLines { get; set; }

        /// <summary>Gets or sets the marker size</summary>
        public MarkerType Marker { get; set; }

        /// <summary>Marker size.</summary>
        public MarkerSizeType MarkerSize { get; set; }

        /// <summary>Gets or sets the line type to show</summary>
        public LineType Line { get; set; }

        /// <summary>Gets or sets the line thickness</summary>
        public LineThicknessType LineThickness { get; set; }

        /// <summary>Gets or sets the checkpoint to get data from.</summary>
        public string Checkpoint { get; set; }

        /// <summary>Gets or sets the name of the table to get data from.</summary>
        public string TableName { get; set; }

        /// <summary>Gets or sets the name of the x field</summary>
        public string XFieldName { get; set; }

        /// <summary>Gets or sets the name of the y field</summary>
        public string YFieldName { get; set; }

        /// <summary>Gets or sets the name of the x2 field</summary>
        public string X2FieldName { get; set; }

        /// <summary>Gets or sets the name of the y2 field</summary>
        public string Y2FieldName { get; set; }

        /// <summary>Gets or sets a value indicating whether the series should be shown in the legend</summary>
        public bool ShowInLegend { get; set; }

        /// <summary>Gets or sets a value indicating whether the series name should be shown in the legend</summary>
        public bool IncludeSeriesNameInLegend { get; set; }

        /// <summary>Gets or sets a value indicating whether the Y variables should be cumulative.</summary>
        public bool Cumulative { get; set; }

        /// <summary>Gets or sets a value indicating whether the X variables should be cumulative.</summary>
        public bool CumulativeX { get; set; }

        /// <summary>Optional data filter.</summary>
        public string Filter { get; set; }
        
        /// <summary>A list of all factors that can be listed as 'vary by' in markers/line types etc.</summary>
        [XmlIgnore]
        public List<string> FactorNamesForVarying { get; set; }

        /// <summary>Called by the graph presenter to get a list of all actual series to put on the graph.</summary>
        /// <param name="definitions">A list of definitions to add to.</param>
        /// <param name="storage">Storage service</param>
        public void GetSeriesToPutOnGraph(IStorageReader storage, List<SeriesDefinition> definitions)
        {
            List<SeriesDefinition> ourDefinitions = new List<SeriesDefinition>();

            // If this series doesn't have a table name then it must be getting its data from other models.
            if (TableName == null)
                ourDefinitions.Add(CreateDefinition(Name, null, Colour, Marker, Line, null));
            else
            {
                // Find a parent that heads the scope that we're going to graph
                IModel parent = FindParent();

                List<ISimulationGeneratorFactor> simulationZones = null;
                do
                {
                    // Create a list of all simulation/zone objects that we're going to graph.
                    simulationZones = BuildListFromModel(parent);
                    parent = parent.Parent;
                }
                while (simulationZones.Count == 0 && parent != null);

                // Get rid of factors that don't vary across objects.
                simulationZones = MergeFactors(simulationZones);
                FactorNamesForVarying = simulationZones.Select(f => f.Name).Distinct().ToList();

                // Check for old .apsimx that doesn't vary colours, lines or markers but has experiments. In this 
                // new code, we want to make this situation explicit and say we'll vary colours and markers by
                // experiment.
                if (FactorIndexToVaryColours == -1 && FactorIndexToVaryLines == -1 && FactorIndexToVaryMarkers == -1 &&
                    FactorNamesForVarying.Contains("Experiment") && !ColourUtilities.Colours.Contains(Colour))
                {
                    FactorIndexToVaryColours = FactorNamesForVarying.IndexOf("Experiment");
                    FactorIndexToVaryMarkers = FactorIndexToVaryColours;
                }
                else if (!ColourUtilities.Colours.Contains(Colour))
                {
                    Colour = ColourUtilities.Colours[0];
                }

                // If a factor isn't being used to vary a colour/marker/line, then remove the factor. i.e. we
                // don't care about it.
                simulationZones = RemoveFactorsNotBeingUsed(simulationZones);

                // Get data for each simulation / zone object
                DataTable baseData = GetBaseData(storage, simulationZones);
                if (baseData != null && baseData.Rows.Count > 0)
                    ourDefinitions = ConvertToSeriesDefinitions(simulationZones, storage, baseData);
            }

            // We might have child models that want to add to our series definitions e.g. regression.
            foreach (IGraphable series in Apsim.Children(this, typeof(IGraphable)))
                series.GetSeriesToPutOnGraph(storage, ourDefinitions);

            // Remove series that have no data.
            ourDefinitions.RemoveAll(d => !MathUtilities.ValuesInArray(d.x) || !MathUtilities.ValuesInArray(d.y));

            definitions.AddRange(ourDefinitions);
        }

        ///// <summary>
        ///// Go through all simulation zone objects and remove factors that don't vary between objects.
        ///// </summary>
        ///// <param name="simulationZones">A list of simulation zones.</param>
        //private List<string> GetFactorList(List<SimulationZone> simulationZones)
        //{
        //    List<string> factorNames = new List<string>();
        //    foreach (SimulationZone simZone in simulationZones)
        //        foreach (KeyValuePair<string, string> factorPair in simZone.factorNameValues)
        //            factorNames.Add(factorPair.Key);
        //    return factorNames.Distinct().ToList();
        //}

        /// <summary>
        /// Go through all factors and try to merge the ones that have the same Name, Value and ColumnName
        /// </summary>
        /// <param name="factors">A list of factors to potentially merge.</param>
        /// <returns>A new list of factors</returns>
        private List<ISimulationGeneratorFactor> MergeFactors(List<ISimulationGeneratorFactor> factors)
        {
            List<ISimulationGeneratorFactor> newFactors = new List<ISimulationGeneratorFactor>();
            foreach (var factor in factors)
            {
                var existingFactor = newFactors.Find(f => f.Equals(factor));
                if (existingFactor == null)
                    newFactors.Add(factor);
                else
                    existingFactor.Merge(factor); 
            }
            return newFactors;
        }

        /// <summary>
        /// Remove factors that aren't being used to vary visual elements (e.g. line/marker etc)
        /// </summary>
        /// <param name="factors">A list of simulation zones.</param>
        private List<ISimulationGeneratorFactor> RemoveFactorsNotBeingUsed(List<ISimulationGeneratorFactor> factors)
        {
            List<string> factorsToKeep = new List<string>();
            if (FactorIndexToVaryColours >= 0 && FactorIndexToVaryColours < FactorNamesForVarying.Count)
                factorsToKeep.Add(FactorNamesForVarying[FactorIndexToVaryColours]);
            if (FactorIndexToVaryLines >= 0 && FactorIndexToVaryLines < FactorNamesForVarying.Count)
                factorsToKeep.Add(FactorNamesForVarying[FactorIndexToVaryLines]);
            if (FactorIndexToVaryMarkers >= 0 && FactorIndexToVaryMarkers < FactorNamesForVarying.Count)
                factorsToKeep.Add(FactorNamesForVarying[FactorIndexToVaryMarkers]);

            return factors.FindAll(factor => factorsToKeep.Contains(factor.Name));
        }

        ///// <summary>
        ///// Remove the specified factor from all simulation/zone objects and then merge
        ///// all identical objects.
        ///// </summary>
        ///// <param name="simulationZones"></param>
        ///// <param name="factorToIgnore"></param>
        //private List<SimulationZone> RemoveFactorAndMerge(List<SimulationZone> simulationZones, string factorToIgnore)
        //{
        //    simulationZones.ForEach(simZone => simZone.RemoveFactor(factorToIgnore));
        //    List<SimulationZone> newList = simulationZones.Distinct().ToList();
        //    foreach (SimulationZone simZone in newList)
        //    {
        //        foreach (SimulationZone duplicate in simulationZones.FindAll(s => s.Equals(simZone)))
        //            duplicate.simulationNames.ForEach(simName => simZone.AddSimulationName(simName));
        //    }
        //    return newList;
        //}

        /// <summary>Find a parent to base our series on.</summary>
        private IModel FindParent()
        {
            Type[] parentTypesToMatch = new Type[] { typeof(Simulation), typeof(Zone), typeof(Experiment),
                                                     typeof(Folder), typeof(Simulations) };

            IModel obj = Parent;
            do
            {
                foreach (Type typeToMatch in parentTypesToMatch)
                    if (typeToMatch.IsAssignableFrom(obj.GetType()))
                        return obj;
                obj = obj.Parent;
            }
            while (obj != null);
            return obj;
        }

        /// <summary>
        /// Create graph definitions for the specified model
        /// </summary>
        /// <param name="model"></param>
        private List<ISimulationGeneratorFactor> BuildListFromModel(IModel model)
        {
            var simulationZonePairs = new List<ISimulationGeneratorFactor>();
            if (model is ISimulationGenerator)
                simulationZonePairs.AddRange((model as ISimulationGenerator).GetFactors());
            else
            {
                foreach (IModel child in model.Children)
                {
                    if (child is Simulation || child is ISimulationGenerator || child is Folder)
                        simulationZonePairs.AddRange(BuildListFromModel(child));
                }
            }
            return simulationZonePairs;
        }

        ///// <summary>
        ///// Build a list of simulation / zone pairs from the specified simulation
        ///// </summary>
        ///// <param name="model">This can be either a simulation or a zone</param>
        ///// <returns>A list of simulation / zone pairs</returns>
        ////private List<SimulationZone> BuildListFromSimulation(IModel model)
        //{
        //    IModel simulation = Apsim.Parent(model, typeof(Simulation));
        //    List<SimulationZone> simulationZonePairs = new List<SimulationZone>();
        //    foreach (Zone zone in Apsim.ChildrenRecursively(model, typeof(Zone)))
        //        simulationZonePairs.Add(new SimulationZone(simulation.Name, zone.Name));

        //    if (typeof(Zone).IsAssignableFrom(model.GetType()))
        //        simulationZonePairs.Add(new SimulationZone(simulation.Name, model.Name));
        //    return simulationZonePairs;
        //}

        ///// <summary>
        ///// Build a list of simulation / zone pairs from the specified experiment
        ///// </summary>
        ///// <param name="model"></param>
        ///// <returns></returns>
        //private List<SimulationZone> BuildListFromExperiment(IModel model)
        //{
        //    List<SimulationZone> simulationZonePairs = new List<SimulationZone>();
        //    foreach (SimulationZone simulationZonePair in BuildListFromSimulation((model as Experiment).BaseSimulation))
        //    {
        //        foreach (List<FactorValue> combination in (model as Experiment).AllCombinations())
        //        {
        //            string zoneName = simulationZonePair.factorNameValues.Find(factorValue => factorValue.Key == "Zone").Value;
        //            SimulationZone simulationZone = new SimulationZone(null, zoneName);
        //            string simulationName = model.Name;
        //            foreach (FactorValue value in combination)
        //            {
        //                simulationName += value.Name;
        //                string factorName = value.Factor.Name;
        //                if (value.Factor.Parent is Factor)
        //                {
        //                    factorName = value.Factor.Parent.Name;
        //                }
        //                string factorValue = value.Name.Replace(factorName, "");
        //                simulationZone.factorNameValues.Add(new KeyValuePair<string, string>(factorName, factorValue));
        //            }
        //            simulationZone.factorNameValues.Add(new KeyValuePair<string, string>("Experiment", (model as Experiment).Name));
        //            simulationZone.simulationNames.Add(simulationName);
        //            simulationZonePairs.Add(simulationZone);
        //        }
        //    }
        //    return simulationZonePairs;
        //}

        ///// <summary>
        ///// Build a list of simulation / zone pairs from the specified experiment
        ///// </summary>
        ///// <param name="simGenerator">Simulation generator</param>
        ///// <returns></returns>
        //private List<SimulationZone> BuildListFromSimulationGenerator(ISimulationGenerator simGenerator)
        //{
        //    List<SimulationZone> simulationZonePairs = new List<SimulationZone>();

        //    // Get a list of simulation names from generator.
        //    IEnumerable simulationNames = simGenerator.GetSimulationNames();

        //    // Get a list of factor names from generator.
        //    List<SimulationGeneratorFactor> factors = simGenerator.GetFactors();

        //    //foreach (string simulationName in simulationNames)
        //        simulationZonePairs.Add(new SimulationZone(null, factors));

        //    return simulationZonePairs;
        //}



        /// <summary>
        /// Paint the visual elements (colour, line and marker) of all simulation / zone pairs.
        /// </summary>
        /// <param name="factors">The simulation/zone pairs to change</param>
        /// <param name="storage">Storage reader</param>
        /// <param name="baseData">Base data</param>
        private List<SeriesDefinition> ConvertToSeriesDefinitions(List<ISimulationGeneratorFactor> factors, IStorageReader storage, DataTable baseData)
        {
            // Create an appropriate painter object
            SimulationZonePainter.IPainter painter;
            if (FactorIndexToVaryColours != -1 && FactorIndexToVaryColours < FactorNamesForVarying.Count)
            {
                string factorNameToVaryByColours = FactorNamesForVarying[FactorIndexToVaryColours];
                if (FactorIndexToVaryLines == FactorIndexToVaryColours)
                    painter = new SimulationZonePainter.SequentialPainterTwoFactors()
                    {
                        FactorName = factorNameToVaryByColours,
                        MaximumIndex1 = ColourUtilities.Colours.Length,
                        MaximumIndex2 = Enum.GetValues(typeof(LineType)).Length - 1, // minus 1 to avoid None type
                        Setter1 = VisualElements.SetColour,
                        Setter2 = VisualElements.SetLineType
                    };
                else if (FactorIndexToVaryMarkers == FactorIndexToVaryColours)
                    painter = new SimulationZonePainter.SequentialPainterTwoFactors()
                    {
                        FactorName = factorNameToVaryByColours,
                        MaximumIndex1 = ColourUtilities.Colours.Length,
                        MaximumIndex2 = Enum.GetValues(typeof(MarkerType)).Length - 1,// minus 1 to avoid None type
                        Setter1 = VisualElements.SetColour,
                        Setter2 = VisualElements.SetMarker
                    };
                else if (FactorIndexToVaryLines != -1)
                    painter = new SimulationZonePainter.DualPainter()
                    {
                        FactorName1 = factorNameToVaryByColours,
                        FactorName2 = FactorNamesForVarying[FactorIndexToVaryLines],
                        MaximumIndex1 = ColourUtilities.Colours.Length,
                        MaximumIndex2 = Enum.GetValues(typeof(LineType)).Length - 1, // minus 1 to avoid None type
                        Setter1 = VisualElements.SetColour,
                        Setter2 = VisualElements.SetLineType
                    };
                else if (FactorIndexToVaryMarkers != -1)
                    painter = new SimulationZonePainter.DualPainter()
                    {
                        FactorName1 = factorNameToVaryByColours,
                        FactorName2 = FactorNamesForVarying[FactorIndexToVaryMarkers],
                        MaximumIndex1 = ColourUtilities.Colours.Length,
                        MaximumIndex2 = Enum.GetValues(typeof(MarkerType)).Length - 1,// minus 1 to avoid None type
                        Setter1 = VisualElements.SetColour,
                        Setter2 = VisualElements.SetMarker
                    };
                else
                    painter = new SimulationZonePainter.SequentialPainter()
                    {
                        FactorName = factorNameToVaryByColours,
                        MaximumIndex = ColourUtilities.Colours.Length,
                        Setter = VisualElements.SetColour
                    };
            }
            else if (FactorIndexToVaryLines != -1 && FactorIndexToVaryLines < FactorNamesForVarying.Count)
            {
                string factorNameToVaryByLine = FactorNamesForVarying[FactorIndexToVaryLines];
                painter = new SimulationZonePainter.SequentialPainter()
                {
                    FactorName = factorNameToVaryByLine,
                    MaximumIndex = Enum.GetValues(typeof(LineType)).Length - 1, // minus 1 to avoid None type   
                    Setter = VisualElements.SetLineType
                };
            }
            else if (FactorIndexToVaryMarkers != -1 && FactorIndexToVaryMarkers < FactorNamesForVarying.Count)
            {
                string factorNameToVaryByMarker = FactorNamesForVarying[FactorIndexToVaryMarkers];
                painter = new SimulationZonePainter.SequentialPainter()
                {
                    FactorName = factorNameToVaryByMarker,
                    MaximumIndex = Enum.GetValues(typeof(MarkerType)).Length - 1,// minus 1 to avoid None type
                    Setter = VisualElements.SetMarker
                };
            }
            else
                painter = new SimulationZonePainter.DefaultPainter() { Colour = Colour, LineType = Line, MarkerType = Marker };

            List<SeriesDefinition> definitions = new List<SeriesDefinition>();
            // Apply the painter to all simulation zone objects.
            foreach (ISimulationGeneratorFactor factor in factors)
            {
                VisualElements visualElement = new VisualElements();
                visualElement.colour = Colour;
                visualElement.Line = Line;
                visualElement.LineThickness = LineThickness;
                visualElement.Marker = Marker;
                visualElement.MarkerSize = MarkerSize;
                painter.PaintSimulationZone(factor, visualElement);

                SeriesDefinition seriesDefinition = new Models.Graph.SeriesDefinition();
                seriesDefinition.type = Type;
                seriesDefinition.marker = visualElement.Marker;
                seriesDefinition.line = visualElement.Line;
                seriesDefinition.markerSize = visualElement.MarkerSize;
                seriesDefinition.lineThickness = visualElement.LineThickness;
                seriesDefinition.colour = visualElement.colour;
                seriesDefinition.xFieldName = XFieldName;
                seriesDefinition.yFieldName = YFieldName;
                seriesDefinition.xAxis = XAxis;
                seriesDefinition.yAxis = YAxis;
                seriesDefinition.xFieldUnits = storage.GetUnits(TableName, XFieldName);
                seriesDefinition.yFieldUnits = storage.GetUnits(TableName, YFieldName);
                seriesDefinition.showInLegend = ShowInLegend;
                seriesDefinition.title = factor.Name + factor.Value;
                if (IncludeSeriesNameInLegend)
                {
                    seriesDefinition.title += ": " + Name;
                }
                if (Checkpoint != "Current")
                    seriesDefinition.title += " (" + Checkpoint + ")";
                DataView data = new DataView(baseData);
                try
                {
                    data.RowFilter = CreateRowFilter(new ISimulationGeneratorFactor[] { factor });
                }
                catch
                {

                }
                if (data.Count > 0)
                {
                    seriesDefinition.data = data.ToTable();
                    seriesDefinition.x = GetDataFromTable(seriesDefinition.data, XFieldName);
                    seriesDefinition.y = GetDataFromTable(seriesDefinition.data, YFieldName);
                    seriesDefinition.x2 = GetDataFromTable(seriesDefinition.data, X2FieldName);
                    seriesDefinition.y2 = GetDataFromTable(seriesDefinition.data, Y2FieldName);
                    seriesDefinition.error = GetErrorDataFromTable(seriesDefinition.data, YFieldName);
                    if (Cumulative)
                        seriesDefinition.y = MathUtilities.Cumulative(seriesDefinition.y as IEnumerable<double>);
                    if (CumulativeX)
                        seriesDefinition.x = MathUtilities.Cumulative(seriesDefinition.x as IEnumerable<double>);
                }
                definitions.Add(seriesDefinition);
            }
            return definitions;
        }

        /// <summary>Creates a series definition.</summary>
        /// <param name="title">The title.</param>
        /// <param name="filter">The filter. Can be null.</param>
        /// <param name="colour">The colour.</param>
        /// <param name="line">The line type.</param>
        /// <param name="marker">The marker type.</param>
        /// <param name="simulationNames">A list of simulations to include in data.</param>
        /// <returns>The newly created definition.</returns>
        private SeriesDefinition CreateDefinition(string title, string filter, Color colour, MarkerType marker, LineType line, string[] simulationNames)
        {
            SeriesDefinition definition = new SeriesDefinition();
            definition.SimulationNames = simulationNames;
            definition.Filter = filter;
            definition.colour = colour;
            definition.title = title;
            if (IncludeSeriesNameInLegend)
                definition.title += ": " + Name;
            definition.line = line;
            definition.marker = marker;
            definition.lineThickness = LineThickness;
            definition.markerSize = MarkerSize;
            definition.showInLegend = ShowInLegend;
            definition.type = Type;
            definition.xAxis = XAxis;
            definition.xFieldName = XFieldName;
            definition.yAxis = YAxis;
            definition.yFieldName = YFieldName;

            // If the table name is null then use reflection to get data from other models.
            if (TableName == null)
            {
                if (!String.IsNullOrEmpty(XFieldName))
                    definition.x = GetDataFromModels(XFieldName);
                if (!String.IsNullOrEmpty(YFieldName))
                    definition.y = GetDataFromModels(YFieldName);
                if (!String.IsNullOrEmpty(X2FieldName))
                    definition.x2 = GetDataFromModels(X2FieldName);
                if (!String.IsNullOrEmpty(Y2FieldName))
                    definition.y2 = GetDataFromModels(Y2FieldName);
            }

            return definition;
        }

        /// <summary>Gets a column of data from a table.</summary>
        /// <param name="data">The table</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns>The column of data.</returns>
        private IEnumerable GetDataFromTable(DataTable data, string fieldName)
        {
            if (fieldName != null && data != null && data.Columns.Contains(fieldName))
            {
                if (data.Columns[fieldName].DataType == typeof(DateTime))
                    return DataTableUtilities.GetColumnAsDates(data, fieldName);
                else if (data.Columns[fieldName].DataType == typeof(string))
                    return DataTableUtilities.GetColumnAsStrings(data, fieldName);
                else
                    return DataTableUtilities.GetColumnAsDoubles(data, fieldName);
            }
            return null;
        }

        /// <summary>Gets a column of error data from a table.</summary>
        /// <param name="data">The table</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns>The column of data.</returns>
        private IEnumerable GetErrorDataFromTable(DataTable data, string fieldName)
        {
            string errorFieldName = fieldName + "Error";
            if (fieldName != null && data != null && data.Columns.Contains(errorFieldName))
            {
                if (data.Columns[errorFieldName].DataType == typeof(double))
                    return DataTableUtilities.GetColumnAsDoubles(data, errorFieldName);
            }
            return null;
        }

        /// <summary>Return data using reflection</summary>
        /// <param name="fieldName">The field name to get data for.</param>
        /// <returns>The return data or null if not found</returns>
        private IEnumerable GetDataFromModels(string fieldName)
        {
            if (fieldName != null && fieldName.StartsWith("["))
            {
                int posCloseBracket = fieldName.IndexOf(']');
                if (posCloseBracket == -1)
                    throw new Exception("Invalid graph field name: " + fieldName);

                string modelName = fieldName.Substring(1, posCloseBracket - 1);
                string namePath = fieldName.Remove(0, posCloseBracket + 2);

                IModel modelWithData = Apsim.Find(this, modelName) as IModel;
                if (modelWithData == null)
                {
                    // Try by assuming the name is a type.
                    Type t = ReflectionUtilities.GetTypeFromUnqualifiedName(modelName);
                    if (t != null)
                    {
                        IModel parentOfGraph = this.Parent.Parent;
                        if (t.IsAssignableFrom(parentOfGraph.GetType()))
                            modelWithData = parentOfGraph;
                        else
                            modelWithData = Apsim.Find(parentOfGraph, t);
                    }
                }

                if (modelWithData != null)
                {
                    // Use reflection to access a property.
                    object obj = Apsim.Get(modelWithData, namePath);
                    if (obj != null && obj.GetType().IsArray)
                        return obj as Array;
                }
            }
            else
            {
                return Apsim.Get(this, fieldName) as IEnumerable;
            }

            return null;
        }

        /// <summary>Called by the graph presenter to get a list of all annotations to put on the graph.</summary>
        /// <param name="annotations">A list of annotations to add to.</param>
        public void GetAnnotationsToPutOnGraph(List<Annotation> annotations)
        {
            // We might have child models that wan't to add to the annotations e.g. regression.
            foreach (IGraphable series in Apsim.Children(this, typeof(IGraphable)))
                series.GetAnnotationsToPutOnGraph(annotations);
        }

        /// <summary>
        /// Create a data view from the specified table and filter.
        /// </summary>
        /// <param name="factors">The list of simulation / zone pairs.</param>
        /// <param name="storage">Storage service</param>
        private DataTable GetBaseData(IStorageReader storage, List<ISimulationGeneratorFactor> factors)
        {
            List<string> fieldNames = new List<string>();
            foreach (ISimulationGeneratorFactor factor in factors)
                fieldNames.Add(factor.ColumnName);
            if (XFieldName != null)
                fieldNames.Add(XFieldName);
            if (YFieldName != null)
                fieldNames.Add(YFieldName);
            if (YFieldName != null)
            {
                if (storage.ColumnNames(TableName).Contains(YFieldName + "Error"))
                    fieldNames.Add(YFieldName + "Error");
            }
            if (X2FieldName != null)
                fieldNames.Add(X2FieldName);
            if (Y2FieldName != null)
                fieldNames.Add(Y2FieldName);

            // Add in column names from annotation series.
            foreach (EventNamesOnGraph annotation in Apsim.Children(this, typeof(EventNamesOnGraph)))
                fieldNames.Add(annotation.ColumnName);

            string filterToUse;
            if (Filter == null)
                filterToUse = CreateRowFilter(factors);
            else
                filterToUse = Filter + " AND (" + CreateRowFilter(factors) + ")";

            return storage.GetData(tableName: TableName, checkpointName: Checkpoint, fieldNames: fieldNames.Distinct(), filter: filterToUse);
        }


        private class ColumnNameValues
        {
            public string ColumnName { get; set; }
            public List<string> ColumnValues { get; set; }

            public ColumnNameValues(string name, List<string> values)
            {
                ColumnName = name;
                ColumnValues = new List<string>();
                ColumnValues.AddRange(values);
            }
        }

        /// <summary>
        /// Create a row filter for the specified factors.
        /// </summary>
        /// <param name="factors">A list of factors to build a filter for</param>
        private string CreateRowFilter(IEnumerable<ISimulationGeneratorFactor> factors)
        {
            string factorFilters = null;

            List<ColumnNameValues> columns = new List<ColumnNameValues>();
            foreach (var factor in factors)
            {
                ColumnNameValues column = columns.Find(col => col.ColumnName == factor.ColumnName);
                if (column == null)
                    columns.Add(new ColumnNameValues(factor.ColumnName, factor.ColumnValues));
                else
                    column.ColumnValues.AddRange(factor.ColumnValues);
            }

            foreach (var column in columns)
            {
                if (factorFilters != null)
                    factorFilters += " OR ";
                if (column.ColumnValues.Count == 1)
                    foreach (var value in column.ColumnValues)
                        factorFilters += column.ColumnName + " = '" + value + "'";
                else
                    factorFilters += column.ColumnName + " IN (" +
                                                  StringUtilities.Build(column.ColumnValues, ",", "'", "'") +
                                                  ")";
            }

            return factorFilters;
        }

        /// <summary>
        /// Represents the visual elements of a series.
        /// </summary>
        class VisualElements
        {
            /// <summary>Gets or set the colour</summary>
            public Color colour { get; set; }

            /// <summary>Gets or sets the marker size</summary>
            public MarkerType Marker { get; set; }

            /// <summary>Marker size.</summary>
            public MarkerSizeType MarkerSize { get; set; }

            /// <summary>Gets or sets the line type to show</summary>
            public LineType Line { get; set; }

            /// <summary>Gets or sets the line thickness</summary>
            public LineThicknessType LineThickness { get; set; }

            /// <summary>A static setter function for colour from an index</summary>
            /// <param name="visualElement">The visual element to change</param>
            /// <param name="index">The index</param>
            public static void SetColour(VisualElements visualElement, int index)
            {
                visualElement.colour = ColourUtilities.Colours[index];
            }

            /// <summary>A static setter function for line type from an index</summary>
            /// <param name="visualElement">The visual element to change</param>
            /// <param name="index">The index</param>
            public static void SetLineType(VisualElements visualElement, int index)
            {
                visualElement.Line = (LineType)Enum.GetValues(typeof(LineType)).GetValue(index);
            }

            /// <summary>A static setter function for marker from an index</summary>
            /// <param name="visualElement">The visual element to change</param>
            /// <param name="index">The index</param>
            public static void SetMarker(VisualElements visualElement, int index)
            {
                visualElement.Marker = (MarkerType)Enum.GetValues(typeof(MarkerType)).GetValue(index);
            }
        }

        /// <summary>
        /// This class paints (sets visual elements) of a group of simulation zone objects.
        /// </summary>
        class SimulationZonePainter
        {
            /// <summary>A delegate setter function.</summary>
            /// <param name="visualElement">The visual element to change</param>
            /// <param name="index">The index</param>
            public delegate void SetFunction(VisualElements visualElement, int index);

            /// <summary>A painter interface for setting visual elements of a simulation/zone pair</summary>
            public interface IPainter
            {
                void PaintSimulationZone(ISimulationGeneratorFactor factor, VisualElements visualElement);
            }

            /// <summary>A default painter for setting a simulation / zone pair to default values.</summary>
            public class DefaultPainter : IPainter
            {
                public Color Colour { get; set; }
                public LineType LineType { get; set; }
                public MarkerType MarkerType { get; set; }
                public void PaintSimulationZone(ISimulationGeneratorFactor factor, VisualElements visualElement)
                {
                    visualElement.colour = Colour;
                    visualElement.Line = LineType;
                    visualElement.Marker = MarkerType;
                }
            }

            /// <summary>A painter for setting a simulation / zone pair to consecutive values of a visual element.</summary>
            public class SequentialPainter : IPainter
            {
                private List<string> values = new List<string>();
                public string FactorName { get; set; }
                public int MaximumIndex { get; set; }
                public SetFunction Setter { get; set; }

                public void PaintSimulationZone(ISimulationGeneratorFactor factor, VisualElements visualElement)
                {
                    string factorValue = factor.Value;
                    int index = values.IndexOf(factorValue);
                    if (index == -1)
                    {
                        values.Add(factorValue);
                        index = values.Count - 1;
                    }
                    index = index % MaximumIndex;
                    Setter(visualElement, index);
                }
            }

            /// <summary>A painter for setting a simulation / zone pair to consecutive values of two visual elements.</summary>
            public class SequentialPainterTwoFactors : IPainter
            {
                private List<string> values = new List<string>();

                public int MaximumIndex1 { get; set; }
                public int MaximumIndex2 { get; set; }
                public string FactorName { get; set; }
                public SetFunction Setter1 { get; set; }
                public SetFunction Setter2 { get; set; }

                public void PaintSimulationZone(ISimulationGeneratorFactor factor, VisualElements visualElement)
                {
                    string factorValue = factor.Value;

                    int index1 = values.IndexOf(factorValue);
                    if (index1 == -1)
                    {
                        values.Add(factorValue);
                        index1 = values.Count - 1;
                    }
                    int index2 = index1 / MaximumIndex1;
                    index2 = index2 % MaximumIndex2;
                    index1 = index1 % MaximumIndex1;
                    Setter1(visualElement, index1);
                    Setter2(visualElement, index2);
                }
            }

            /// <summary>A painter for setting a simulation / zone pair to values of two visual elements.</summary>
            public class DualPainter : IPainter
            {
                private List<string> values1 = new List<string>();
                private List<string> values2 = new List<string>();

                public int MaximumIndex1 { get; set; }
                public int MaximumIndex2 { get; set; }
                public string FactorName1 { get; set; }
                public string FactorName2 { get; set; }
                public SetFunction Setter1 { get; set; }
                public SetFunction Setter2 { get; set; }

                public void PaintSimulationZone(ISimulationGeneratorFactor factor, VisualElements visualElement)
                {
                    string factorValue1 = null; // = simulationZonePair.GetValueOf(FactorName1);
                    string factorValue2 = null; // simulationZonePair.GetValueOf(FactorName2);

                    int index1 = values1.IndexOf(factorValue1);
                    if (index1 == -1)
                    {
                        values1.Add(factorValue1);
                        index1 = values1.Count - 1;
                    }

                    int index2 = values2.IndexOf(factorValue2);
                    if (index2 == -1)
                    {
                        values2.Add(factorValue2);
                        index2 = values2.Count - 1;
                    }

                    index1 = index1 % MaximumIndex1;
                    index2 = index2 % MaximumIndex2;
                    Setter1(visualElement, index1);
                    Setter2(visualElement, index2);
                }
            }
        }

    }
}
