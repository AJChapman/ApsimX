﻿using Models.CLEM.Groupings;
using Models.CLEM.Interfaces;
using Models.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Models.CLEM
{
    /// <summary>
    /// Implements IFilterGroup for a specific set of filter parameters
    /// </summary>
    [Serializable]
    public abstract class FilterGroup<TFilter> : CLEMModel, IFilterGroup
        where TFilter : IFilterable
    {
        [NonSerialized]
        private protected IEnumerable<Func<IFilterable, bool>> filterRules = null;
        [NonSerialized]
        private protected IEnumerable<ISort> sortList = null;

        /// <summary>
        /// The properties available for filtering
        /// </summary>
        [NonSerialized]
        protected Dictionary<string, PropertyInfo> properties;

        /// <inheritdoc/>
        [JsonIgnore]
        public IEnumerable<string> Parameters => properties?.Keys;

        /// <inheritdoc/>
        public IEnumerable<string> GetParameterNames()
        {
            if (properties is null)
                InitialiseFilters(false);

            return properties.Keys;
        }


        /// <inheritdoc/>
        public PropertyInfo GetProperty(string name) 
        {
            if (properties is null)
                InitialiseFilters(false);

            return properties[name]; 
        }

        /// <summary>
        /// Clear all rules
        /// </summary>
        public void ClearRules()
        {
            foreach (Filter filter in FindAllChildren<Filter>())
                filter.ClearRule();
        }

        ///<inheritdoc/>
        [EventSubscribe("Commencing")]
        protected void OnSimulationCommencing(object sender, EventArgs e)
        {
            filterRules = null;
            sortList = null;
            InitialiseFilters();
        }

        /// <summary>
        /// Initialise filter rules and dropdown lists of properties available for TFilter
        /// </summary>
        public void InitialiseFilters(bool includeBuildRules = true)
        {
            properties = typeof(TFilter)
                .GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                .Where(prop => Attribute.IsDefined(prop, typeof(FilterByPropertyAttribute)))
                .ToDictionary(prop => prop.Name, prop => prop);

            var types = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.Contains(nameof(Models.CLEM)))
                .Where(t => t.IsSubclassOf(typeof(TFilter)));

            foreach (var type in types)
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                                    .Where(prop => Attribute.IsDefined(prop, typeof(FilterByPropertyAttribute)));
                foreach (var prop in props)
                {
                    string key = prop.DeclaringType.Name;
                    if (key.StartsWith(typeof(TFilter).Name))
                        key = key.Substring(typeof(TFilter).Name.Length);
                    properties.Add($"{key}.{prop.Name}", prop);
                }
            }

            foreach (Filter filter in FindAllChildren<Filter>())
            {
                filter.Initialise();
                if (includeBuildRules)
                    filter.BuildRule();
            }

            sortList = FindAllChildren<ISort>();
        }

        /// <inheritdoc/>
        public virtual IEnumerable<T> Filter<T>(IEnumerable<T> source) where T : IFilterable
        {
            if (source is null)
                throw new NullReferenceException("Cannot filter a null object");

            filterRules ??= FindAllChildren<Filter>().Select(filter => filter.Rule);

            var filtered = filterRules.Any() ? source.Where(item => filterRules.All(rule => rule is null ? false : rule(item))) : source;

            if(sortList?.Any()??false)
                // add sorting and take specified
                filtered = filtered.Sort(sortList);

            // do all takes and skips
            foreach (var take in FindAllChildren<TakeFromFiltered>())
            {
                int number = 0;
                switch (take.TakeStyle)
                {
                    case TakeFromFilterStyle.TakeProportion:
                    case TakeFromFilterStyle.SkipProportion:
                        number = take.NumberToTake(filtered.Count());
                        break;
                    case TakeFromFilterStyle.TakeIndividuals:
                    case TakeFromFilterStyle.SkipIndividuals:
                        number = take.NumberToTake(0);
                        break;
                }
                switch (take.TakeStyle)
                {
                    case TakeFromFilterStyle.TakeProportion:
                    case TakeFromFilterStyle.TakeIndividuals:
                        if (take.TakePositionStyle == TakeFromFilteredPositionStyle.Start)
                            filtered = filtered.Take(number);
                        else
                            filtered = filtered.TakeLast(number);
                        break;
                    case TakeFromFilterStyle.SkipProportion:
                    case TakeFromFilterStyle.SkipIndividuals:
                        if (take.TakePositionStyle == TakeFromFilteredPositionStyle.Start)
                            filtered = filtered.Skip(number);
                        else
                            filtered = filtered.SkipLast(number);
                        break;
                }
            }
            return filtered;
        }

        ///<inheritdoc/>
        public virtual bool Filter<T>(T item) where T : IFilterable
        {
            if (item == null)
                throw new NullReferenceException("Cannot filter a null object");

            filterRules ??= FindAllChildren<Filter>().Select(filter => filter.Rule);

            return filterRules.All(rule => rule is null ? false : rule(item));
        }

        #region descriptive summary

        /// <summary>
        /// Provides the closing html tags for object
        /// </summary>
        /// <returns></returns>
        public override string ModelSummaryInnerClosingTags()
        {
            return "\r\n</div>";
        }

        /// <summary>
        /// Provides the closing html tags for object
        /// </summary>
        /// <returns></returns>
        public override string ModelSummaryInnerOpeningTags()
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"filterborder clearfix\">");
                if (FindAllChildren<Filter>().Count() == 0)
                    htmlWriter.Write("<div class=\"filter\">All individuals</div>");

                return htmlWriter.ToString();
            }
        }
        #endregion
    }
}
