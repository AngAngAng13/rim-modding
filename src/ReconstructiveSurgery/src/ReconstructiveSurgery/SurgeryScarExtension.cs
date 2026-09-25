using System.Collections.Generic;
using Verse;

namespace ReconstructiveSurgery
{
    public class SurgeryScarExtension : DefModExtension
    {
        public List<string> allowedInjuryDefs = new List<string>();

        public bool requireOutsideOnly = true;

        public bool requireInsideOnly;
    }
}
