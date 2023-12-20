using Elements;
using Elements.GeoJSON;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Spatial;
using Elements.Validators;
using Elements.Serialization.JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Elements.Geometry.Line;
using Polygon = Elements.Geometry.Polygon;

namespace Elements
{
    public class PackingResult : Element
    {
        public int SolutionNumber;
        public string SolutionPattern;
        public double Remainder;

        public PackingResult(int solutionNumber, string solutionPattern, double remainder)
        {
            this.SolutionNumber = solutionNumber;
            this.SolutionPattern = solutionPattern;
            this.Remainder = remainder;
        }

    }
}