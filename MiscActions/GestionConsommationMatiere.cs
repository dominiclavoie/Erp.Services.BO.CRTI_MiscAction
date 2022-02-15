extern alias issueReturnContract;

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
    class GestionConsommationMatiere : BaseService
    {
        private issueReturnContract.Erp.Tablesets.IssueReturnTableset ds;
        private issueReturnContract.Erp.Tablesets.SelectedJobAsmblTableset selDs;
        private issueReturnContract.Erp.Contracts.IssueReturnSvcContract svc;
        private StockProfileBrut spb;
        private bool plNegQtyAction;
        private string tranType;
        private string callProcess;

        public GestionConsommationMatiere(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session)
        {
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<issueReturnContract.Erp.Contracts.IssueReturnSvcContract>(Db);
            this.spb = new StockProfileBrut(this.Db, this.Session);
        }
        public bool IssueMaterial(string job, string company, string mtlSeq, decimal tranQty, string lotNum, string issuedPartNum, out string partTranPKs, out string message)
        {
            message = string.Empty;
            partTranPKs = string.Empty;
            this.tranType = "STK-MTL";
            this.callProcess = "IssueMaterial";
            if (!GetNewJobAsmbl(job, company, out message))
            {
                return false;
            }
            if (!SetMtlSeq(mtlSeq, out message))
            {
                return false;
            }
            if (!SetIssuedPartNum(issuedPartNum, out message))
            {
                return false;
            }
            if (!SetTranQty(tranQty, out message))
            {
                return false;
            }
            if (!SetLotNum(lotNum, out message))
            {
                return false;
            }
            if (!PerformMaterialMovement(out partTranPKs, out message))
            {
                return false;
            }
            return true;
        }

        public bool ReturnMaterial(string job, string company, string mtlSeq, decimal tranQty, string lotNum, string issuedPartNum, out string partTranPKs, out string message)
        {
            message = string.Empty;
            partTranPKs = string.Empty;
            this.tranType = "MTL-STK";
            this.callProcess = "ReturnMaterial";
            if (!GetNewJobAsmbl(job, company, out message))
            {
                return false;
            }
            if (!SetMtlSeq(mtlSeq, out message))
            {
                return false;
            }
            if (!SetIssuedPartNum(issuedPartNum, out message))
            {
                return false;
            }
            if (!SetTranQty(tranQty, out message))
            {
                return false;
            }
            if (!SetLotNum(lotNum, out message))
            {
                return false;
            }
            if (!PerformMaterialMovement(out partTranPKs, out message))
            {
                return false;
            }
            return true;
        }

        private bool GetNew(out string message)
        {
            message = string.Empty;
            try
            {
                this.ds = new issueReturnContract.Erp.Tablesets.IssueReturnTableset();
                Guid empty = Guid.Empty;
                this.svc.GetNewIssueReturn(this.tranType, empty, this.callProcess, ref this.ds);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool GetNewJobAsmbl(string job, string company, out string message)
        {
            message = string.Empty;
            try
            {
                this.selDs = new issueReturnContract.Erp.Tablesets.SelectedJobAsmblTableset();
                issueReturnContract.Erp.Tablesets.SelectedJobAsmblRow dataRow = new issueReturnContract.Erp.Tablesets.SelectedJobAsmblRow();
                dataRow.JobNum = job;
                dataRow.Company = company;
                dataRow.AssemblySeq = 0;
                dataRow.RowMod = "A";
                this.selDs.SelectedJobAsmbl.Add(dataRow);
                Guid empty = Guid.Empty;
                string pcMessage = "";
                this.ds = this.svc.GetNewJobAsmblMultiple(this.tranType, empty, this.callProcess, ref this.selDs, out pcMessage);
                this.ds.IssueReturn.FirstOrDefault().RowMod = "U";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool SetMtlSeq(string mtlSeq, out string message)
        {
            message = string.Empty;
            try
            {
                if (this.callProcess == "IssueMaterial")
                {
                    this.svc.OnChangingToJobSeq(int.Parse(mtlSeq), ref this.ds);
                }
                if (this.callProcess == "ReturnMaterial")
                {
                    this.svc.OnChangingJobSeq(int.Parse(mtlSeq), "From", this.callProcess, ref this.ds);
                }
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                string pcMessage = "";
                if(this.callProcess == "IssueMaterial")
                {
                    row.ToJobSeq = int.Parse(mtlSeq);
                    this.svc.OnChangeToJobSeq(ref this.ds, this.callProcess, out pcMessage);
                }
                else
                {
                    row.FromJobSeq = int.Parse(mtlSeq);
                    this.svc.OnChangeFromJobSeq(ref this.ds, this.callProcess, out pcMessage);
                }
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool SetIssuedPartNum(string issuedPartNum, out string message)
        {
            message = string.Empty;
            try
            {
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                if(row.PartNum == issuedPartNum)
                {
                    return true;
                }
                row.PartNum = issuedPartNum;
                this.svc.OnChangePartNum(ref this.ds, this.callProcess);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool SetTranQty(decimal tranQty, out string message)
        {
            message = string.Empty;
            try
            {
                this.svc.OnChangeTranQty(tranQty, ref this.ds);
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                row.TranQty = tranQty;
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool SetLotNum(string lotNum, out string message)
        {
            message = string.Empty;
            try
            {
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                if (!row.PartTrackLots)
                {
                    return true;
                }
                this.svc.OnChangeLotNum(lotNum, ref this.ds);
                row.LotNum = lotNum;
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool SetWarehouseCode(string whseCode, out string message)
        {
            message = string.Empty;
            try
            {
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                if (this.callProcess == "IssueMaterial")
                {
                    row.FromWarehouseCode = whseCode;
                    this.svc.OnChangeFromWarehouse(ref this.ds, this.callProcess);
                }
                if (this.callProcess == "ReturnMaterial")
                {
                    row.ToWarehouseCode = whseCode;
                    this.svc.OnChangeToWarehouse(ref this.ds, this.callProcess);
                }
                row.RowMod = "U";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool SetBinNum(string binNum, out string message)
        {
            message = string.Empty;
            try
            {
                string pcMessage;
                bool plOverrideBinChange = false;
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                if (this.callProcess == "IssueMaterial")
                {
                    this.svc.OnChangingFromBinNum(ref this.ds, out pcMessage);
                    if (!string.IsNullOrEmpty(pcMessage))
                    {
                        plOverrideBinChange = true;
                    }
                    row.FromBinNum = binNum;
                    this.svc.OnChangeFromBinNum(plOverrideBinChange, ref this.ds);
                }
                if (this.callProcess == "ReturnMaterial")
                {
                    row.ToBinNum = binNum;
                }
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool PerformMaterialMovement(out string partTranPKs, out string message)
        {
            message = string.Empty;
            partTranPKs = string.Empty;
            try
            {
                issueReturnContract.Erp.Tablesets.IssueReturnRow row = this.ds.IssueReturn.Where(tt => tt.Updated()).FirstOrDefault();
                string partNum = row["PartNum"].ToString();
                string lotNum = row["LotNum"].ToString();
                this.spb.GetProfileBrutLocalisation(partNum, lotNum);
                if (this.spb.DsMiscAction.Tables["LocalisationProfileBrut"].Rows.Count == 0)
                {
                    throw new BLException("La localisation de la pièce est introuvable. Veuillez contacter l'administrateur du système.");
                }
                DataRow locRow = this.spb.DsMiscAction.Tables["LocalisationProfileBrut"].Rows[0];
                string whse = locRow["WarehouseCode"].ToString();
                string bin = locRow["BinNum"].ToString();
                if(!SetWarehouseCode(whse, out message))
                {
                    return false;
                }
                if(!SetBinNum(bin, out message))
                {
                    return false;
                }
                bool requiresUserInput = false;
                this.svc.PrePerformMaterialMovement(ref this.ds, out requiresUserInput);
                if (!MasterInventoryBinTests())
                {
                    return false;
                }
                string legalNumberMessage = "";
                this.svc.PerformMaterialMovement(plNegQtyAction, ref this.ds, out legalNumberMessage, out partTranPKs);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool MasterInventoryBinTests()
        {
            bool flag = true;
            string pcNeqQtyAction = string.Empty;
            string pcNeqQtyMessage = string.Empty;
            string pcPCBinAction = string.Empty;
            string pcPCBinMessage = string.Empty;
            string pcOutBinAction = string.Empty;
            string pcOutBinMessage = string.Empty;
            this.svc.MasterInventoryBinTests(ref this.ds, out pcNeqQtyAction, out pcNeqQtyMessage, out pcPCBinAction, out pcPCBinMessage, out pcOutBinAction, out pcOutBinMessage);
            if (pcNeqQtyMessage != "")
            {
                switch (pcNeqQtyAction.ToUpper())
                {
                    case "STOP":
                        plNegQtyAction = false;
                        flag = false;
                        break;
                    case "NONE":
                        plNegQtyAction = false;
                        flag = true;
                        break;
                    case "ASK USER":
                        plNegQtyAction = true;
                        flag = true;
                        break;
                }
                if (!flag)
                {
                    return flag;
                }
            }
            if (pcPCBinMessage != "")
            {
                switch (pcPCBinAction.ToUpper())
                {
                    case "STOP":
                        flag = false;
                        break;
                    case "NONE":
                        flag = true;
                        break;
                    case "WARN":
                        flag = true;
                        break;
                }
                if (!flag)
                {
                    return flag;
                }
            }
            if (pcOutBinMessage != "")
            {
                switch (pcOutBinAction.ToUpper())
                {
                    case "STOP":
                        flag = false;
                        break;
                    case "NONE":
                        flag = true;
                        break;
                    case "WARN":
                        flag = true;
                        break;
                }
            }
            return flag;
        }

        public void Dispose()
        {
            this.svc.Dispose();
            this.svc = null;
            this.spb = null;
            this.ds = null;
            this.selDs = null;
        }
        
    }
    
}
