using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob
{
    public class OpenJobToJobCollection
    {
        public IEnumerable<OpenJobToJob> openJobs { get; set; }
        public OpenJobToJobCollection() { }
    }
}
