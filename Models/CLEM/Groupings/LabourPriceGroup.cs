﻿using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Models.CLEM.Groupings
{
    ///<summary>
    /// Contains a group of filters to identify individual labour in a set price group
    ///</summary> 
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(LabourPricing))]
    [Description("Set the pay rate for the selected group of individuals")]
    [Version(1, 0, 1, "")]
    [HelpUri(@"Content/Features/Filters/Groups/LabourPriceGroup.htm")]
    public class LabourPriceGroup : FilterGroup<LabourType>
    {
        /// <summary>
        /// Pay rate
        /// </summary>
        [Description("Daily pay rate")]
        [Required, GreaterThanEqualValue(0)]
        public double Value { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        protected LabourPriceGroup()
        {
            base.ModelSummaryStyle = HTMLSummaryStyle.SubResource;
        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary()
        {
            string html = "";
            if (!FormatForParentControl)
            {
                html += "\r\n<div class=\"activityentry\">";
                html += "Pay ";
                if (Value.ToString() == "0")
                {
                    html += "<span class=\"errorlink\">NOT SET";
                }
                else
                {
                    html += "<span class=\"setvalue\">";
                    html += Value.ToString("#,0.##");
                }
                html += "</span> for a days work";
                html += "</div>";
            }
            return html;
        }

        /// <inheritdoc/>
        public override string ModelSummaryInnerClosingTags()
        {
            string html = "";
            if (FormatForParentControl)
            {
                if (Value.ToString() == "0")
                {
                    html += "</td><td><span class=\"errorlink\">NOT SET";
                }
                else
                {
                    html += "</td><td><span class=\"setvalue\">";
                    html += this.Value.ToString("#,0.##");
                }
                html += "</span></td>";
                html += "</tr>";
            }
            else
            {
                html += "\r\n</div>";
            }
            return html;
        }

        /// <inheritdoc/>
        public override string ModelSummaryInnerOpeningTags()
        {
            string html = "";
            if (FormatForParentControl)            
                html += "<tr><td>" + this.Name + "</td><td>";
            else            
                html += "\r\n<div class=\"filterborder clearfix\">";            

            if (FindAllChildren<Filter>().Count() < 1)
                html += "<div class=\"filter\">All individuals</div>";

            return html;
        }

        /// <inheritdoc/>
        public override string ModelSummaryClosingTags()
        {
            return !FormatForParentControl ? base.ModelSummaryClosingTags() : "";
        }

        /// <inheritdoc/>
        public override string ModelSummaryOpeningTags()
        {
            return !FormatForParentControl ? base.ModelSummaryOpeningTags() : "";
        } 
        #endregion
    }
}
