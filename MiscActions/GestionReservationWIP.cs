using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;
using Epicor.Data;
using Ice;
using Ice.Tables;
using Ice.Tablesets;

namespace Erp.BO.CRTI_MiscAction
{
    class GestionReservationWIP : BaseMiscAction, IMiscAction
    {
        public GestionReservationWIP(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        private void GetNewReservation(string idLigneFrom, string idLigneTo, string jobNumTo, string mtlSeq, string partNum, string lotNum)
        {
            int increment = 0;
            var rsv = (from ln in Db.UD104
                       where ln.Key1 == idLigneFrom &&
                             ln.Key2 == "Reserve" &&
                             ln.Key3 == idLigneTo &&
                             ln.Key4 == mtlSeq &&
                             ln.Key5 != null &&
                             ln.Key5 != "" &&
                             ln.Company == this.Session.CompanyID
                       select ln).ToList()
                        .OrderByDescending(x => Convert.ToInt32(x.Key5)).FirstOrDefault();
            if (rsv == null)
            {
                increment = 1;
            }
            else
            {
                increment = Convert.ToInt32(rsv.Key5) + 1;
            }

            Ice.Tablesets.UD104Tableset dsReservation = new UD104Tableset();
            Ice.Contracts.UD104SvcContract svcReservation = Ice.Assemblies.ServiceRenderer.GetService<Ice.Contracts.UD104SvcContract>(this.Db);
            try
            {
                svcReservation.GetaNewUD104(ref dsReservation);
                Ice.Tablesets.UD104Row udReservation = dsReservation.UD104.Where(tt => tt.Added()).FirstOrDefault();
                if (udReservation == null)
                {
                    throw new BLException("Impossible de créer la réservation.");
                }
                string now = DateTime.Now.ToString("yyyyMMddHHmmss");
                udReservation.Key1 = idLigneFrom;
                udReservation.Key2 = "Reserve";
                udReservation.Key3 = idLigneTo;
                udReservation.Key4 = mtlSeq;
                udReservation.Key5 = increment.ToString();
                udReservation.Character01 = partNum;
                udReservation.Character02 = lotNum;
                udReservation.Character03 = now;
                udReservation.ShortChar01 = jobNumTo;
                udReservation.Date01 = DateTime.Today;
                svcReservation.Update(ref dsReservation);
            }
            catch (Exception) { return; }
            finally
            {
                dsReservation = null;
                svcReservation.Dispose();
                svcReservation = null;
            }
        }
        private UD104 GetReservation(string idLigneFrom, string idLigneTo, string mtlSeq, string partNum, string lotNum)
        {
            return (from ud in this.Db.UD104
                    where ud.Company == this.Session.CompanyID &&
                                  ud.Key1 == idLigneFrom &&
                                  ud.Key2 == "Reserve" &&
                                  ud.Key3 == idLigneTo &&
                                  ud.Key4 == mtlSeq &&
                                  ud.Character01 == partNum &&
                                  ud.Character02 == lotNum
                    select ud).FirstOrDefault();
        }
        private bool GetReservations(string partTranSysRowID, out IEnumerable<UD104> reservations, out decimal tranQty)
        {
            reservations = null;
            tranQty = 0m;
            var rsv = (from pt in this.Db.PartTran.AsEnumerable()
                       join ud in this.Db.UD104.AsEnumerable() on new { pt.Company, pt.PartNum, pt.LotNum, Type = "Reserve" }
                                                           equals new { ud.Company, PartNum = ud.Character01, LotNum = ud.Character02, Type = ud.Key2 }
                       where pt.Company == this.Session.CompanyID &&
                             pt.SysRowID.ToString() == partTranSysRowID &&
                             ud.Number03 > 0m
                       select new
                       {
                           Reservation = ud,
                           Quantity = pt.TranQty,
                           TimeStamp = ud.Character03
                       }).OrderBy(x => Convert.ToInt64(x.TimeStamp));
            if (!rsv.Any())
            {
                return false;
            }
            reservations = rsv.Select(tt => tt.Reservation);
            tranQty = rsv.First().Quantity;
            return true;
        }
        public void Reserver(string idLigneFrom, string idLigneTo, string jobNum, string mtlSeq, string partNum, string lotNum, decimal quantity)
        {
            StockProfileBrut stockProfileBrut = new StockProfileBrut(this.Db, this.Session);
            if (!stockProfileBrut.CheckWIPQuantityAvailable(partNum, lotNum, idLigneFrom, quantity))
            {
                throw new BLException("Il n'y a pas suffisamment de quantités produites en cours pour ce numéro de lot.");
            }
            UD104 reservation = GetReservation(idLigneFrom, idLigneTo, mtlSeq, partNum, lotNum);
            if (reservation == null)
            {
                GetNewReservation(idLigneFrom, idLigneTo, jobNum, mtlSeq, partNum, lotNum);
                reservation = GetReservation(idLigneFrom, idLigneTo, mtlSeq, partNum, lotNum);
                if (reservation == null)
                {
                    throw new BLException("Impossible de récupérer la réservation.");
                }
            }

            using (var txScope = IceContext.CreateDefaultTransactionScope())
            {
                reservation.Number02 += quantity;
                reservation.Number03 += quantity;
                Db.Validate<UD104>(reservation);
                txScope.Complete();
            }
        }
        public void Annuler(string idLigneFrom, string idLigneTo, string mtlSeq, string partNum, string lotNum, decimal quantity)
        {
            UD104 reservation = GetReservation(idLigneFrom, idLigneTo, mtlSeq, partNum, lotNum);
            if (reservation == null)
            {
                throw new BLException("Impossible de récupérer la réservation.");
            }
            if (quantity > reservation.Number02)
            {
                throw new BLException("Impossible d'enlever plus que la quantité qui était réservée.");
            }
            using (var txScope = IceContext.CreateDefaultTransactionScope())
            {
                reservation.Number02 -= quantity;
                reservation.Number03 -= quantity;
                Db.Validate<UD104>(reservation);
                txScope.Complete();
            }
        }
        private void TraiterReservation(UD104 reservation, string partTranPKs, decimal tranQty)
        {
            Ice.Tablesets.UD104Tableset dsReservation = new UD104Tableset();
            Ice.Contracts.UD104SvcContract svcReservation = Ice.Assemblies.ServiceRenderer.GetService<Ice.Contracts.UD104SvcContract>(this.Db);
            try
            {
                svcReservation.GetaNewUD104A(ref dsReservation, reservation.Key1, reservation.Key2, reservation.Key3, reservation.Key4, reservation.Key5);
                Ice.Tablesets.UD104ARow udReservation = dsReservation.UD104A.Where(tt => tt.Added()).FirstOrDefault();
                if (udReservation == null)
                {
                    throw new BLException("Impossible de créer la réservation.");
                }
                string now = DateTime.Now.ToString("yyyyMMddHHmmss");
                udReservation.ChildKey2 = partTranPKs;
                udReservation.ChildKey3 = now;
                udReservation.Number01 = tranQty;
                udReservation.Date01 = DateTime.Today;
                svcReservation.Update(ref dsReservation);

                dsReservation = svcReservation.GetByID(reservation.Key1, reservation.Key2, reservation.Key3, reservation.Key4, reservation.Key5);
                Ice.Tablesets.UD104Row headReservation = dsReservation.UD104.FirstOrDefault();
                headReservation.RowMod = "U";
                headReservation.Number03 -= tranQty;
                svcReservation.Update(ref dsReservation);
            }
            catch (Exception) { return; }
            finally
            {
                dsReservation = null;
                svcReservation.Dispose();
                svcReservation = null;
            }
        }
        public void Traiter(string partTranSysRowID)
        {
            IEnumerable<UD104> reservations;
            decimal tranQty;
            if(!GetReservations(partTranSysRowID, out reservations, out tranQty))
            {
                return;
            }
            decimal qte = tranQty;
            GestionConsommationMatiere gestionConsommationMatiere = new GestionConsommationMatiere(this.Db, this.Session);
            try
            {
                foreach(UD104 rsv in reservations)
                {
                    decimal issueQty = rsv.Number03 < qte ? rsv.Number03 : qte;
                    string partTranPKs;
                    string message;
                    if(!gestionConsommationMatiere.IssueMaterial(rsv.ShortChar01, this.Session.CompanyID, rsv.Key4, issueQty, rsv.Character02, rsv.Character01, out partTranPKs, out message))
                    {
                        throw new BLException(message);
                        //continue;
                    }
                    TraiterReservation(rsv, partTranPKs, issueQty);
                    qte -= issueQty;
                    if(qte <= 0m)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) { throw new BLException(ex.Message); }
            finally
            {
                gestionConsommationMatiere.Dispose();
            }
        }

    }
}
