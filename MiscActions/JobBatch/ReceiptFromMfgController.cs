using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;
using Epicor.Data;
using Ice;
using Ice.Tables;
using Ice.Tablesets;

namespace Erp.BO.CRTI_MiscAction
{
    class ReceiptFromMfgController : BaseService
    {
        private Erp.Tablesets.ReceiptsFromMfgTableset ds;
        private Erp.Contracts.ReceiptsFromMfgSvcContract svc;
        private Erp.Internal.Lib.RoundAmountEF LibRoundAmountEF;

        public ReceiptFromMfgController(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session)
        {
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.ReceiptsFromMfgSvcContract>(Db);
            this.LibRoundAmountEF = new Internal.Lib.RoundAmountEF(Db);
        }
        public void Dispose()
        {
            this.svc.Dispose();
            this.svc = null;
            this.LibRoundAmountEF.Dispose();
            this.LibRoundAmountEF = null;
        }
        public bool ReceiveToJob(string jobNum, int assm, string lotNum, decimal qte, string jobNum2, int assm2, int jobSeq2, decimal costProportion = 1m)
        {
            string partTranPK;
            bool flag = ReceiveMfgPartToJob(jobNum, assm, lotNum, qte, jobNum2, assm2, jobSeq2, out partTranPK, costProportion);
            return flag;
        }
        private bool ReceiveMfgPartToJob(string jobNum, int assm, string lotNum, decimal qte, string jobNum2, int assm2, int jobSeq2, out string partTranPK, decimal costProportion)
        {
            partTranPK = string.Empty;
            
            this.ds = new ReceiptsFromMfgTableset();
            try
            {
                string pcTranType = "MFG-WIP";
                svc.GetNewReceiptsFromMfgJobAsm(jobNum, assm, pcTranType, "RcptToJobEntry", ref this.ds);
                PartTranRow newRow = this.ds.PartTran.Where(tt => tt.Company == this.Session.CompanyID).FirstOrDefault();
                if(newRow == null)
                {
                    return false;
                }
                newRow.RowMod = "U";
                newRow.ActTranQty = qte;
                newRow.LotNum = lotNum;
                string pcMessage = "";
                string errormsg = "";
                this.svc.OnChangeLotNum(ref this.ds, false, lotNum, out pcMessage, out errormsg);
                if (errormsg != "")
                {
                    this.svc.OnChangeLotNum(ref this.ds, true, lotNum, out pcMessage, out errormsg);
                }
                pcMessage = "";
                this.svc.OnChangeActTranQty(ref this.ds, out pcMessage);
                decimal num = newRow.MtlUnitCost;
                decimal num2 = newRow.LbrUnitCost;
                decimal num3 = newRow.BurUnitCost;
                decimal num4 = newRow.SubUnitCost;
                decimal num5 = newRow.MtlBurUnitCost;
                this.svc.OnChangeOverrideCost(ref this.ds, true);
                newRow.MtlUnitCost = num * costProportion;
                newRow.LbrUnitCost = num2 * costProportion;
                newRow.BurUnitCost = num3 * costProportion;
                newRow.SubUnitCost = num4 * costProportion;
                newRow.MtlBurUnitCost = num5 * costProportion;
                newRow.ExtMtlCost = LibRoundAmountEF.RoundDecimalsApply(num * costProportion * qte, "", "PartTran", "ExtCost");
                newRow.ExtSubCost = LibRoundAmountEF.RoundDecimalsApply(num2 * costProportion * qte, "", "PartTran", "ExtCost");
                newRow.ExtLbrCost = LibRoundAmountEF.RoundDecimalsApply(num3 * costProportion * qte, "", "PartTran", "ExtCost");
                newRow.ExtBurCost = LibRoundAmountEF.RoundDecimalsApply(num4 * costProportion * qte, "", "PartTran", "ExtCost");
                newRow.ExtMtlBurCost = LibRoundAmountEF.RoundDecimalsApply(num5 * costProportion * qte, "", "PartTran", "ExtCost");
                newRow.JobNum2 = jobNum2;
                pcMessage = "";
                this.svc.OnChangeJobNum2(ref this.ds, out pcMessage);
                newRow.AssemblySeq2 = assm2;
                pcMessage = "";
                this.svc.OnChangeAssemblySeq2(ref this.ds, out pcMessage);
                newRow.JobSeq2 = jobSeq2;
                pcMessage = "";
                this.svc.OnChangeJobSeq2(ref this.ds, out pcMessage);
                newRow.WareHouseCode = newRow.WareHouse2;
                newRow.BinNum = newRow.BinNum2;
                bool requiresUserInput = false;
                this.svc.PreUpdate(ref this.ds, out requiresUserInput);
                decimal pdSerialNoQty = default(decimal);
                bool plNegQtyAction = true;
                string pcMessage2 = "";
                bool issuedComplete = false;
                this.svc.ReceiveMfgPartToJob(ref this.ds, pdSerialNoQty, plNegQtyAction, issuedComplete, out pcMessage2, out partTranPK, "RcptToJobEntry");
                return true;
            }
            catch (Exception) { return false; }
            finally
            {
                this.ds = null;
            }
        }
    }
    class ReceiptFromMfgCostSplitter
    {
        private List<CostSplitter> costSplitters;
        public ReceiptFromMfgCostSplitter()
        {
            this.costSplitters = new List<CostSplitter>();
        }
        public void Add(string jobNum, string opCode, decimal qty)
        {
            CostSplitter costSplitter = this.costSplitters.Where(tt => tt.JobNum == jobNum && tt.OpCode == opCode).FirstOrDefault();
            if(costSplitter == null)
            {
                costSplitter = new CostSplitter(jobNum, opCode);
                this.costSplitters.Add(costSplitter);
            }
            costSplitter.AddQty(qty);
        }
        public decimal GetRemainingQty(string jobNum, string opCode)
        {
            decimal remainingQty = 0m;
            CostSplitter costSplitter = this.costSplitters.Where(tt => tt.JobNum == jobNum && tt.OpCode == opCode).FirstOrDefault();
            if(costSplitter != null)
            {
                remainingQty = costSplitter.TotalQty;
            }
            return remainingQty;
        }
        public void TransferWIP(string jobNum, string opCode, decimal qty)
        {
            CostSplitter costSplitter = this.costSplitters.Where(tt => tt.JobNum == jobNum && tt.OpCode == opCode).FirstOrDefault();
            if (costSplitter != null)
            {
                costSplitter.SubtractQty(qty);
            }
        }

        private class CostSplitter
        {
            #region Properties
            private string jobNum;
            private string opCode;
            private decimal totalQty;
            public string JobNum { get => jobNum; }
            public string OpCode { get => opCode; }
            public decimal TotalQty { get => totalQty; }
            #endregion

            public CostSplitter(string _jobNum, string _opCode)
            {
                this.jobNum = _jobNum;
                this.opCode = _opCode;
            }

            public void AddQty(decimal qty)
            {
                this.totalQty += qty;
            }
            public void SubtractQty(decimal qty)
            {
                this.totalQty -= qty;
                if (this.totalQty < 0m)
                    this.totalQty = 0m;
            }

        }
    }
}
