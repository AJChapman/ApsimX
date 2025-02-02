﻿using System;
using System.Collections.Generic;
using Models.Core;
using Models.Interfaces;
using APSIM.Shared.Utilities;
using Models.PMF.Organs;
using APSIM.Shared.Documentation;

namespace Models.Functions.RootShape
{
    /// <summary>
    /// This model calculates the proportion of each soil layer occupided by roots.
    /// </summary>
    [Serializable]
    [ValidParent(ParentType = typeof(Root))]
    public class RootShapeCylinder : Model, IRootShape
    {
        /// <summary>Calculates the root area for a layer of soil</summary>
        public void CalcRootProportionInLayers(ZoneState zone)
        {
            var physical = zone.Soil.FindChild<Soils.IPhysical>();
            zone.RootArea = (zone.RightDist + zone.LeftDist) * zone.Depth / 1e6;
            for (int layer = 0; layer < physical.Thickness.Length; layer++)
            {
                double prop;
                double top = layer == 0 ? 0 : MathUtilities.Sum(physical.Thickness, 0, layer - 1);

                if (zone.Depth < top)
                {
                    prop = 0;
                } 
                else
                {
                    prop = SoilUtilities.ProportionThroughLayer(physical.Thickness, layer, zone.Depth);
                }
                zone.RootProportions[layer] = prop;
            }
        }

        /// <summary>Document the model.</summary>
        public override IEnumerable<ITag> Document()
        {
            // Write description of this class from summary and remarks XML documentation.
            foreach (var tag in GetModelDescription())
                yield return tag;

            foreach (var tag in DocumentChildren<IModel>())
                yield return tag;
        }
    }
}
