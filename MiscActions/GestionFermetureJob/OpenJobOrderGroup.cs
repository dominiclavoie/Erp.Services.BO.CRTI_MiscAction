using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob
{
    public class OpenJobOrderGroup
    {
        public int OrderNum { get; set; }
        public string CustomerName { get; set; }
        public DateTime ReqDate { get; set; }
        public OpenJobOrderGroup() { }
    }
}
