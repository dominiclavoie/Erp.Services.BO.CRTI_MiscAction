using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob
{
    public class OpenJobInventoryCollection
    {
        public IEnumerable<OpenJobInventory> openJobs { get; set; }
        public OpenJobInventoryCollection() { }
    }
}
