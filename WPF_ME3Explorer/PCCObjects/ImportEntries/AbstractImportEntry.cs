using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPF_ME3Explorer.PCCObjects;

namespace WPF_ME3Explorer.PCCObjects.ImportEntries
{
    public abstract class AbstractImportEntry
    {
        #region Creation
        /// <summary>
        /// Creates an instance of ImportEntry for specified game.
        /// </summary>
        /// <param name="game">Desired game instance</param>
        /// <param name="pcc">PCC to get Entry from/for.</param>
        /// <param name="stream">(ME3 only) Stream contanining import data</param>
        public static AbstractImportEntry Create(int game, AbstractPCCObject pcc, Stream stream)
        {
            AbstractImportEntry entry = null;
            switch (game)
            {
                case 1:
                    entry = new ME1ImportEntry((ME1PCCObject)pcc);
                    break;
                case 2:
                    entry = new ME2ImportEntry((ME2PCCObject)pcc);
                    break;
                case 3:
                    entry = new ME3ImportEntry((ME3PCCObject)pcc, stream);
                    break;
            }

            return entry;
        }

        /// <summary>
        /// Creates an instance of ImportEntry for specified game.
        /// </summary>
        /// <param name="GameVersion">Desired game instance</param>
        /// <param name="pcc">PCC to get Entry from/for</param>
        public static AbstractImportEntry Create(int GameVersion, AbstractPCCObject pcc)
        {
            AbstractImportEntry entry = null;
            switch (GameVersion)
            {
                case 1:
                    entry = new ME1ImportEntry((ME1PCCObject)pcc);
                    break;
                case 2:
                    entry = new ME2ImportEntry((ME2PCCObject)pcc);
                    break;
                case 3:
                    entry = new ME3ImportEntry((ME3PCCObject)pcc);
                    break;
            }

            return entry;
        }


        protected AbstractImportEntry(AbstractPCCObject pcc)
        {
            pccRef = pcc;
        }
        #endregion


        public string Package { get; set; }
        public int link { get; set; }
        public string Name { get; set; }
        public byte[] raw { get; set; }
        public AbstractPCCObject pccRef { get; set; }
        public virtual int ObjectNameID { get; set; }
    }
}
