using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ivanti_PS_Profile_MigrationGUI
{
   sealed class AppItem
     {
         public string DisplayName { get; }
         public string InternalName { get; }
         public AppItem(string display, string internalName)
         {
             DisplayName = display;
             InternalName = internalName;
         }
         public override string ToString() => DisplayName; // what CheckedListBox shows
     }
    
}
