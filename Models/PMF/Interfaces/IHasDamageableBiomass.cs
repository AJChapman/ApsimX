﻿namespace Models.PMF.Interfaces
{
    using System.Collections.Generic;

    /// <summary>Interface used by models (e.g. STOCK, pests and diseases) to damage a biomass (e.g. plant or surface residues).</summary>
    public interface IHasDamageableBiomass
    {
        /// <summary>Name of plant that can be damaged.</summary>
        string Name { get; }

        /// <summary>A list of material (biomass) that can be damaged.</summary>
        IEnumerable<DamageableBiomass> Material { get; }

        /// <summary>
        /// Remove biomass from an organ.
        /// </summary>
        /// <param name="materialName">Name of organ.</param>
        /// <param name="biomassRemoveType">Name of event that triggered this biomass remove call.</param>
        /// <param name="biomassToRemove">Biomass to remove.</param>
        void RemoveBiomass(string materialName, string biomassRemoveType, OrganBiomassRemovalType biomassToRemove);
    }

    /// <summary>A class to hold a mass of biomass and its digestibility.</summary>
    public class DamageableBiomass
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="biomass">Biomass.</param>
        /// <param name="isLive">Is biomass live.</param>
        public DamageableBiomass(Biomass biomass, bool isLive)
        {
            Biomass = biomass;
            IsLive = isLive;
        }

        /// <summary>Name of material.</summary>
        public string Name { get; }

        /// <summary>Biomass</summary>
        public Biomass Biomass { get; }

        /// <summary>Is biomass live.</summary>
        public bool IsLive { get; }
    }
}