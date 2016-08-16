using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer.PCCObjectsAndBits
{
    public class CustomProperty
    {
        private string sName = string.Empty;
        private string sCat = string.Empty;
        private bool bReadOnly = false;
        private bool bVisible = true;
        private object objValue = null;


        public CustomProperty(string sName, string Category, object value, Type type, bool bReadOnly, bool bVisible)
        {
            this.sName = sName;
            this.sCat = Category;
            this.objValue = value;
            this.type = type;
            this.bReadOnly = bReadOnly;
            this.bVisible = bVisible;
        }

        private Type type;
        public Type Type
        {
            get { return type; }
        }

        public bool ReadOnly
        {
            get
            {
                return bReadOnly;
            }
        }

        public string Name
        {
            get
            {
                return sName;
            }
        }

        public string Category
        {
            get
            {
                return sCat;
            }
        }

        public bool Visible
        {
            get
            {
                return bVisible;
            }
        }

        public object Value
        {
            get
            {
                return objValue;
            }
            set
            {
                objValue = value;
            }
        }

    }
}
