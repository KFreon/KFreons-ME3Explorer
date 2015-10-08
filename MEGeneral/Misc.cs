using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MEGeneral
{
    public static class Methods
    {
        /// <summary>
        /// KFreon:
        /// This is required to correctly get the version from anywhere in the application.
        /// As this Project is referenced by almost all other Projects, I update its Assembly information
        ///    with the current program version. Thus, this method must be used to obtain the correct version.
        /// </summary>
        /// <returns>Current program version.</returns>
        public static string GetBuildVersion()
        {
            return UsefulThings.General.GetStartingVersion();
        }
    }
}
