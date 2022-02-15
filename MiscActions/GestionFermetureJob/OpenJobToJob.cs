using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob
{
    public class OpenJobToJob
    {
        public DateTime ReqDate { get; set; }
        public string JobNum { get; set; }
        public string JobNum2 { get; set; }
        public int AssemblySeq2 { get; set; }
        public int JobSeq2 { get; set; }
        public string PartNum { get; set; }
        public string OpCode { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal RemainingQty { get; set; }
        public bool IsFinalOper { get; set; }
        public string ProdDetail { get; set; }
        public OpenJobToJob() { }
        public object[] GetValues() { return new object[] { false, ReqDate, JobNum, JobNum2, AssemblySeq2, JobSeq2, PartNum, OpCode, RequiredQty, RemainingQty, IsFinalOper, ProdDetail, Guid.NewGuid() }; }
    }
}
