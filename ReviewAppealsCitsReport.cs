namespace Bars.GkhGji.Report.ReviewAppealsCits
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Bars.B4;
    using Bars.B4.Modules.ReportPanel.PrintForm;
    using Bars.B4.Reports;
    using Bars.B4.Utils;
    using Bars.Gkh.Entities;
    using Bars.Gkh.Enums;
    using Bars.GkhGji.Entities;
    using Bars.GkhGji.Enums;

    using Castle.Windsor;

    /// <summary>
    /// Рассмотрение обращений граждан
    /// </summary>
    public class ReviewAppealsCitsReport : BasePrintForm
    {
        private DateTime dateStart = DateTime.MinValue;

        private int year;

        private DateTime dateEnd = DateTime.MaxValue;

        private int[] municipalityIds;

        public ReviewAppealsCitsReport()
            : base(new ReportTemplateBinary(Properties.Resources.ReviewAppealsCits))
        {
        }

        public IWindsorContainer Container { get; set; }

        public override string RequiredPermission
        {
            get
            {
                return "Reports.GJI.ReviewAppealsCits";
            }
        }

        public override string Name
        {
            get { return "Рассмотрение обращений граждан"; }
        }

        public override string Desciption
        {
            get { return "Рассмотрение обращений граждан"; }
        }

        public override string GroupName
        {
            get { return "Обращения ГЖИ"; }
        }

        public override string ParamsController
        {
            get { return "B4.controller.report.ReviewAppealsCits"; }
        }

        public override void SetUserParams(BaseParams baseParams)
        {
            dateStart = baseParams.Params["dateStart"].ToDateTime();
            year = dateStart.Year;
            dateEnd = baseParams.Params["dateEnd"].ToDateTime();

            var m = baseParams.Params["municipalityIds"].ToString();
            this.municipalityIds = !string.IsNullOrEmpty(m) ? m.Split(',').Select(x => x.ToInt()).ToArray() : new int[0];
        }

        public override void PrepareReport(ReportParams reportParams)
        {
            var servAppealCits = Container.Resolve<IDomainService<AppealCits>>();
            var servAppealCitsSource = Container.Resolve<IDomainService<AppealCitsSource>>();
            var servAppealCitsRealityObject = Container.Resolve<IDomainService<AppealCitsRealityObject>>();
            var servBaseStatementAppealCits = Container.Resolve<IDomainService<InspectionAppealCits>>();
            var servDisposal = Container.Resolve<IDomainService<Disposal>>();
            var servProtocol = Container.Resolve<IDomainService<Protocol>>();
            var servPrescription = Container.Resolve<IDomainService<Prescription>>();
            var servActCheck = Container.Resolve<IDomainService<ActCheck>>();
            var servResolution = Container.Resolve<IDomainService<Resolution>>();
            

            var zonalByMuIdDict = Container.Resolve<IDomainService<ZonalInspectionMunicipality>>().GetAll()
                .Select(x => new { ZonalInspectionId = x.ZonalInspection.Id, muId = x.Municipality.Id })
                .AsEnumerable()
                .GroupBy(x => x.muId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.ZonalInspectionId).FirstOrDefault());

            // Инспекции с муниципальными образованиями
            var zonalInspectionDict = this.GetZonalInspectionDict();


            // 2.1 Всего поступило обращений с начала года
            var appealCitsForZonInspDict = this.GetAppealCitsForZonalInspDict(servAppealCits, servAppealCitsRealityObject, zonalByMuIdDict);

            // 2.1.1 - 2.1.4
            var appealCitsForRevenueFormDict = this.GetAppealCitsForRevenueForDict(servAppealCitsSource, servAppealCitsRealityObject);

            // 2.2 Данные по ГЖИРТ с разбивкой по районам ЗЖИ
            var appealCitsForInspectionRtDict = this.GetAppealCitsForInspRt(zonalInspectionDict, servAppealCitsSource, servAppealCitsRealityObject);

            // 3 Рассмотрено с выездом на место
            var notPlannedExitDict = this.GetNotPlannedExit(servBaseStatementAppealCits, servAppealCitsRealityObject, servDisposal, servActCheck, zonalByMuIdDict);

            // 3.1 Выдано предписаний
            var countPrescriptionDict = this.GetCountPrescription(servBaseStatementAppealCits, servAppealCitsRealityObject, servDisposal, servPrescription, zonalByMuIdDict);

            // 3.2 оформлено протоколов (первично)
            var countProtocolDict = this.GetCountProtocol(servBaseStatementAppealCits, servAppealCitsRealityObject, servDisposal, servProtocol, zonalByMuIdDict);

            // 3.3 - составлено актов обследования
            var countActCheckDict = this.GetCountActCheck(servBaseStatementAppealCits, servAppealCitsRealityObject, servDisposal, servActCheck, zonalByMuIdDict);

            // 4.1 и 5.1 составлено актов о выполнении предписаний  (они по логике идентичны тк в первой ветка актов проверки предписания быть не может)
            var countActRemovalDict = servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null && x.baseAppeal.AppealCits.DateFrom != null)
                .Where(x => x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Select(y => new
                {
                    BaseStatementId = y.baseAppeal.Inspection.Id,
                    CountActRemoval = this.Container.Resolve<IDomainService<ActRemoval>>().GetAll()
                    .Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id
                    && x.Stage.Parent.TypeStage == TypeStage.DisposalPrescription
                    && x.TypeRemoval == YesNoNotSet.Yes),
                    muId = y.appealRealObj.RealityObject.Municipality.Id
                })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountActRemoval).Sum());

            // 4.2 оформлено протоколов по ст. 19.5
            var countProtocol195FirstDict = this.GetCountProtocol195First(servBaseStatementAppealCits, servAppealCitsRealityObject, servProtocol, zonalByMuIdDict);

            // 4.3 вынесено постановлений мировыми судьями
            var countResolutionCourtFirstDict = servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                             && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => servResolution.GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountResolution = servResolution.GetAll()
                        .Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.Stage.Parent.TypeStage == TypeStage.DisposalPrescription
                            && x.TypeInitiativeOrg == TypeInitiativeOrgGji.Court),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountResolution).Sum());

            // 4.4 сумма штрафов мирового суда
            var sumResolutionCourtFirstDict = servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                            && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => servResolution.GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        PenaltyAmount = servResolution.GetAll()
                        .Where(x => x.PenaltyAmount != null && x.Inspection.Id == y.baseAppeal.Inspection.Id
                                && x.TypeInitiativeOrg == TypeInitiativeOrgGji.Court
                                && x.Stage.Parent.TypeStage == TypeStage.DisposalPrescription)
                        .Sum(x => x.PenaltyAmount),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                    .AsEnumerable()
                    .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                    .ToDictionary(x => x.Key, x => x.Where(y => y.PenaltyAmount.HasValue).Distinct(y => y.BaseStatementId).Select(y => y.PenaltyAmount.Value).Sum());

            // 4.5 - оформлено протоколов по ст. 7.21, 7.22, 7.23 (вторично)
            var countProtocolArticleLawsFirstDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                             && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
               .Where(y => servProtocol.GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountProtocol = this.Container.Resolve<IDomainService<ProtocolArticleLaw>>().GetAll()
                        .Count(x => x.Protocol.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.Protocol.Stage.Parent.TypeStage == TypeStage.DisposalPrescription
                            && (x.ArticleLaw.Code == "2" || x.ArticleLaw.Code == "3" || x.ArticleLaw.Code == "4" || x.ArticleLaw.Code == "5")),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountProtocol).Sum());

            // 4.6 вынесено постановлений ГЖИ РТ
            var countResolutionHousingInspectionFirstDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => Container.Resolve<IDomainService<Resolution>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountResolution = this.Container.Resolve<IDomainService<Resolution>>().GetAll()
                        .Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.TypeInitiativeOrg == TypeInitiativeOrgGji.HousingInspection
                            && x.Stage.Parent.TypeStage == TypeStage.DisposalPrescription),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountResolution).Sum());

            // 4.7 сумма штрафов ГЖИ
            var sumResolutionHousingInspectionFirstDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => Container.Resolve<IDomainService<Resolution>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        IsResolutionDispute = this.Container.Resolve<IDomainService<ResolutionDispute>>().GetAll().Any(x => x.Resolution.Inspection.Id == y.baseAppeal.Inspection.Id),
                        PenaltyAmount = this.Container.Resolve<IDomainService<Resolution>>().GetAll()
                        .Where(x => x.PenaltyAmount != null
                            && x.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.TypeInitiativeOrg == TypeInitiativeOrgGji.HousingInspection
                            && x.Sanction.Code == "1"
                            && x.Stage.Parent.TypeStage == TypeStage.DisposalPrescription)
                        .Sum(x => x.PenaltyAmount),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                    .AsEnumerable()
                    .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                    .ToDictionary(x => x.Key, x => x.Where(y => !y.IsResolutionDispute && y.PenaltyAmount.HasValue).Distinct(y => y.BaseStatementId).Select(y => y.PenaltyAmount.Value).Sum());

            // 4.8 - снято с контроля (решено положительно) (используется в 5.8)
            var removedFromControlDict = this.GetRemovedFromControl(servBaseStatementAppealCits, servAppealCitsRealityObject, zonalByMuIdDict);

            // 5.2 оформлено протоколов по ст. 19.5
            var countProtocol195Dict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => servProtocol.GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountProtocol = this.Container.Resolve<IDomainService<ProtocolArticleLaw>>().GetAll()
                        .Count(x => x.Protocol.Inspection.Id == y.baseAppeal.Inspection.Id && (x.ArticleLaw.Code == "1" || x.ArticleLaw.Code == "7")),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountProtocol).Sum());

            // 5.3 вынесено постановлений мировыми судьями
            var countResolutionCourtDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => servResolution.GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountResolution = servResolution.GetAll()
                        .Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id && x.TypeInitiativeOrg == TypeInitiativeOrgGji.Court),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountResolution).Sum());

            // 5.4 сумма штрафов мирового суда
            var sumResolutionCourtDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => Container.Resolve<IDomainService<Resolution>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        PenaltyAmount = this.Container.Resolve<IDomainService<Resolution>>().GetAll()
                        .Where(x => x.PenaltyAmount != null && x.Inspection.Id == y.baseAppeal.Inspection.Id && x.TypeInitiativeOrg == TypeInitiativeOrgGji.Court)
                        .Sum(x => x.PenaltyAmount),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                    .AsEnumerable()
                    .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                    .ToDictionary(x => x.Key, x => x.Where(y => y.PenaltyAmount.HasValue).Distinct(y => y.BaseStatementId).Select(y => y.PenaltyAmount.Value).Sum());

            // 5.5 - оформлено протоколов по ст. 7.21, 7.22, 7.23 (вторично)
            var countProtocolArticleLawsDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => servProtocol.GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountProtocol = this.Container.Resolve<IDomainService<ProtocolArticleLaw>>().GetAll()
                        .Count(x => x.Protocol.Inspection.Id == y.baseAppeal.Inspection.Id
                            && (x.ArticleLaw.Code == "2" || x.ArticleLaw.Code == "3" || x.ArticleLaw.Code == "4" || x.ArticleLaw.Code == "5")),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountProtocol).Sum());

            // 5.6 вынесено постановлений ГЖИ РТ
            var countResolutionHousingInspectionDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => Container.Resolve<IDomainService<Resolution>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        CountResolution = this.Container.Resolve<IDomainService<Resolution>>().GetAll()
                        .Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.TypeInitiativeOrg == TypeInitiativeOrgGji.HousingInspection),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                    .AsEnumerable()
                    .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                    .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountResolution).Sum());

            // 5.7 сумма штрафов ГЖИ
            var sumResolutionHousingInspectionDict = servBaseStatementAppealCits.GetAll()
               .Join(
               servAppealCitsRealityObject.GetAll(),
               x => x.AppealCits.Id,
               y => y.AppealCits.Id,
               (a, b) => new { baseAppeal = a, appealRealObj = b })
               .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= dateStart && x.baseAppeal.AppealCits.DateFrom <= dateEnd)
                .Where(y => Container.Resolve<IDomainService<Resolution>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        IsResolutionDispute = this.Container.Resolve<IDomainService<ResolutionDispute>>().GetAll().Any(x => x.Resolution.Inspection.Id == y.baseAppeal.Inspection.Id),
                        PenaltyAmount = this.Container.Resolve<IDomainService<Resolution>>().GetAll()
                        .Where(x => x.PenaltyAmount != null
                            && x.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.TypeInitiativeOrg == TypeInitiativeOrgGji.HousingInspection)
                        .Sum(x => x.PenaltyAmount),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Where(y => !y.IsResolutionDispute && y.PenaltyAmount.HasValue).Distinct(y => y.BaseStatementId).Select(y => y.PenaltyAmount.Value).Sum());

            // 5.8 - снято с контроля (решено положительно)
            var removedFromControlProtocolDict = this.GetRemovedFromControlPlusProtocol(servBaseStatementAppealCits, servAppealCitsRealityObject, zonalByMuIdDict);

            // 6 Направлено обращений для принятия мер
            var countSuretyResolveDict = Container.Resolve<IDomainService<AppealCits>>().GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.Id,
                y => y.AppealCits.Id,
                (a, b) => new { appealCit = a, appealRealObj = b })
                .WhereIf(municipalityIds.Length > 0, x => municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.appealCit.ZonalInspection != null
                     && x.appealCit.DateFrom != null && x.appealCit.DateFrom >= dateStart && x.appealCit.DateFrom <= dateEnd && x.appealCit.SuretyResolve.Code == "4")
                 .Select(y =>
                     new
                     {
                         AppealCitsId = y.appealCit.Id,
                         muId = y.appealRealObj.RealityObject.Municipality.Id
                     })
                     .AsEnumerable()
                     .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                     .ToDictionary(x => x.Key, x => x.Select(y => y.AppealCitsId).Count());

            var takeOnControlDict = this.GetTakeOnControl(servBaseStatementAppealCits, servAppealCitsRealityObject, zonalByMuIdDict);

            var секция = reportParams.ComplexReportParams.ДобавитьСекцию("секцияДанных");

            foreach (var запись in zonalInspectionDict)
            {
                var zonalInspectionId = запись.Key;

                секция.ДобавитьСтроку();
                секция["ЗЖИ"] = запись.Value.ZonalInspection;

                var countPrescription = countPrescriptionDict.ContainsKey(zonalInspectionId) ? countPrescriptionDict[zonalInspectionId] : 0;
                var countProtocol = countProtocolDict.ContainsKey(zonalInspectionId) ? countProtocolDict[zonalInspectionId] : 0;
                var sumResolutionFirstCourt = sumResolutionCourtFirstDict.ContainsKey(zonalInspectionId) ? sumResolutionCourtFirstDict[zonalInspectionId] : 0;
                var sumResolutionHousingInspectionFirst = sumResolutionHousingInspectionFirstDict.ContainsKey(zonalInspectionId) ? sumResolutionHousingInspectionFirstDict[zonalInspectionId] : 0;

                var sumResolutionCourt = sumResolutionCourtDict.ContainsKey(zonalInspectionId) ? sumResolutionCourtDict[zonalInspectionId] : 0;
                var sumResolutionHousingInspection = sumResolutionHousingInspectionDict.ContainsKey(zonalInspectionId) ? sumResolutionHousingInspectionDict[zonalInspectionId] : 0;

                var revenueForm9 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("9") ? appealCitsForRevenueFormDict[zonalInspectionId]["9"] : 0
                    : 0;

                var revenueForm1 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("1") ? appealCitsForRevenueFormDict[zonalInspectionId]["1"] : 0
                    : 0;

                var revenueForm12 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("12") ? appealCitsForRevenueFormDict[zonalInspectionId]["12"] : 0
                    : 0;

                var revenueForm13 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("13") ? appealCitsForRevenueFormDict[zonalInspectionId]["13"] : 0
                    : 0;

                var revenueForm10 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("10") ? appealCitsForRevenueFormDict[zonalInspectionId]["10"] : 0
                    : 0;

                var revenueForm19 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("19") ? appealCitsForRevenueFormDict[zonalInspectionId]["19"] : 0
                    : 0;

                var revenueForm5 = appealCitsForRevenueFormDict.ContainsKey(zonalInspectionId) ?
                    appealCitsForRevenueFormDict[zonalInspectionId].ContainsKey("5") ? appealCitsForRevenueFormDict[zonalInspectionId]["5"] : 0
                    : 0;

                var appealCitsZonInsp = appealCitsForZonInspDict.ContainsKey(zonalInspectionId)
                                            ? appealCitsForZonInspDict[zonalInspectionId]
                                            : 0;

                var appealCitsForInspectionRt = appealCitsForInspectionRtDict.ContainsKey(zonalInspectionId)
                                            ? appealCitsForInspectionRtDict[zonalInspectionId]
                                            : 0;
                var takeOnControl1 = takeOnControlDict.ContainsKey(zonalInspectionId)
                                         ? takeOnControlDict[zonalInspectionId].ContainsKey(year - 1)
                                               ? takeOnControlDict[zonalInspectionId][year - 1]
                                               : 0
                                         : 0;
                var takeOnControl2 = takeOnControlDict.ContainsKey(zonalInspectionId)
                                         ? takeOnControlDict[zonalInspectionId].ContainsKey(year - 2)
                                               ? takeOnControlDict[zonalInspectionId][year - 2]
                                               : 0
                                         : 0;
                var takeOnControl3 = takeOnControlDict.ContainsKey(zonalInspectionId)
                                       ? takeOnControlDict[zonalInspectionId].ContainsKey(year - 3)
                                             ? takeOnControlDict[zonalInspectionId][year - 3]
                                             : 0
                                       : 0;

                var takeOnControl4 = takeOnControlDict.ContainsKey(zonalInspectionId)
                                      ? takeOnControlDict[zonalInspectionId].ContainsKey(year - 4)
                                            ? takeOnControlDict[zonalInspectionId][year - 4]
                                            : 0
                                      : 0;

                секция["Значение1"] = takeOnControl1 + takeOnControl2 + takeOnControl3 + takeOnControl4;
                секция["Значение2"] = appealCitsZonInsp + appealCitsForInspectionRt;
                секция["Значение3"] = appealCitsZonInsp;
                секция["Значение4"] = revenueForm9;
                секция["Значение5"] = revenueForm1;
                секция["Значение6"] = revenueForm12 + revenueForm13 + revenueForm19 + revenueForm10;
                секция["Значение35"] = revenueForm5;

                секция["Значение7"] = appealCitsForInspectionRt;

                секция["Значение8"] = notPlannedExitDict.ContainsKey(zonalInspectionId) ? notPlannedExitDict[zonalInspectionId] : 0;
                секция["Значение9"] = countPrescription;
                секция["Значение10"] = countProtocol;
                секция["Значение11"] = countActCheckDict.ContainsKey(zonalInspectionId) ? countActCheckDict[zonalInspectionId] : 0;
                секция["Значение12"] = countPrescription + countProtocol;

                секция["Значение13"] = countActRemovalDict.ContainsKey(zonalInspectionId) ? countActRemovalDict[zonalInspectionId] : 0;
                секция["Значение14"] = countProtocol195FirstDict.ContainsKey(zonalInspectionId) ? countProtocol195FirstDict[zonalInspectionId] : 0;
                секция["Значение15"] = countResolutionCourtFirstDict.ContainsKey(zonalInspectionId) ? countResolutionCourtFirstDict[zonalInspectionId] : 0;
                секция["Значение16"] = sumResolutionFirstCourt > 0 ? sumResolutionFirstCourt / 1000 : 0;
                секция["Значение17"] = countProtocolArticleLawsFirstDict.ContainsKey(zonalInspectionId) ? countProtocolArticleLawsFirstDict[zonalInspectionId] : 0;
                секция["Значение18"] = countResolutionHousingInspectionFirstDict.ContainsKey(zonalInspectionId) ? countResolutionHousingInspectionFirstDict[zonalInspectionId] : 0;
                секция["Значение19"] = sumResolutionHousingInspectionFirst > 0 ? sumResolutionHousingInspectionFirst / 1000 : 0;
                секция["Значение20"] = removedFromControlDict.ContainsKey(zonalInspectionId) ? removedFromControlDict[zonalInspectionId] : 0;

                секция["Значение21"] = countActRemovalDict.ContainsKey(zonalInspectionId) ? countActRemovalDict[zonalInspectionId] : 0;
                секция["Значение22"] = countProtocol195Dict.ContainsKey(zonalInspectionId) ? countProtocol195Dict[zonalInspectionId] : 0;
                секция["Значение23"] = countResolutionCourtDict.ContainsKey(zonalInspectionId) ? countResolutionCourtDict[zonalInspectionId] : 0;
                секция["Значение24"] = sumResolutionCourt > 0 ? sumResolutionCourt / 1000 : 0;
                секция["Значение25"] = countProtocolArticleLawsDict.ContainsKey(zonalInspectionId) ? countProtocolArticleLawsDict[zonalInspectionId] : 0;
                секция["Значение26"] = countResolutionHousingInspectionDict.ContainsKey(zonalInspectionId) ? countResolutionHousingInspectionDict[zonalInspectionId] : 0;
                секция["Значение27"] = sumResolutionHousingInspection > 0 ? sumResolutionHousingInspection / 1000 : 0;
                секция["Значение28"] = removedFromControlProtocolDict.ContainsKey(zonalInspectionId) ? removedFromControlProtocolDict[zonalInspectionId] : 0;
                секция["Значение29"] = countSuretyResolveDict.ContainsKey(zonalInspectionId) ? countSuretyResolveDict[zonalInspectionId] : 0;

                секция["Значение30"] = takeOnControl1 + takeOnControl2 + takeOnControl3 + takeOnControl4;
                секция["Значение31"] = takeOnControl1;
                секция["Значение32"] = takeOnControl2;
                секция["Значение33"] = takeOnControl3;
                секция["Значение34"] = takeOnControl4;
            }

            var appealCitsZonInspTotal = appealCitsForZonInspDict.Where(x => x.Key != -1).Sum(i => i.Value);
            var appealCitsForInspectionRtTotal = appealCitsForInspectionRtDict.Where(x => x.Key != -1).Sum(i => i.Value);
            var revenueForm9Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("9")).Sum(i => i.Value["9"]);
            var revenueForm1Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("1")).Sum(i => i.Value["1"]);
            var revenueForm12Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("12")).Sum(i => i.Value["12"]);
            var revenueForm13Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("13")).Sum(i => i.Value["13"]);
            var revenueForm19Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("19")).Sum(i => i.Value["19"]);
            var revenueForm10Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("10")).Sum(i => i.Value["10"]);
            var revenueForm5Total = appealCitsForRevenueFormDict.Where(x => x.Key != -1).Where(i => i.Value.ContainsKey("5")).Sum(i => i.Value["5"]);

            var countPrescriptionTotal = countPrescriptionDict.Where(x => x.Key != -1).Sum(i => i.Value);
            var countProtocolTotal = countProtocolDict.Where(x => x.Key != -1).Sum(i => i.Value);
            var sumResolutionFirstTotal = sumResolutionCourtFirstDict.Where(x => x.Key != -1).Sum(i => i.Value);
            var sumResolutionHousingInspectionFirstTotal = sumResolutionHousingInspectionFirstDict.Where(x => x.Key != -1).Sum(i => i.Value);

            var sumResolutionCourtTotal = sumResolutionCourtDict.Where(x => x.Key != -1).Sum(i => i.Value);
            var sumResolutionHousingInspectionTotal = sumResolutionHousingInspectionDict.Where(x => x.Key != -1).Sum(i => i.Value);

            var takeOnControl1Total = takeOnControlDict.Where(x => x.Key != -1).Where(v => v.Value.ContainsKey(year - 1)).Sum(v => v.Value[year - 1]);
            var takeOnControl2Total = takeOnControlDict.Where(x => x.Key != -1).Where(v => v.Value.ContainsKey(year - 2)).Sum(v => v.Value[year - 2]);
            var takeOnControl3Total = takeOnControlDict.Where(x => x.Key != -1).Where(v => v.Value.ContainsKey(year - 3)).Sum(v => v.Value[year - 3]);
            var takeOnControl4Total = takeOnControlDict.Where(x => x.Key != -1).Where(v => v.Value.ContainsKey(year - 4)).Sum(v => v.Value[year - 4]);

            reportParams.SimpleReportParams["Сумма1"] = takeOnControl1Total + takeOnControl2Total + takeOnControl3Total + takeOnControl4Total;
            reportParams.SimpleReportParams["Сумма2"] = appealCitsZonInspTotal + appealCitsForInspectionRtTotal;
            reportParams.SimpleReportParams["Сумма3"] = appealCitsZonInspTotal;
            reportParams.SimpleReportParams["Сумма4"] = revenueForm9Total;
            reportParams.SimpleReportParams["Сумма5"] = revenueForm1Total;
            reportParams.SimpleReportParams["Сумма6"] = revenueForm12Total + revenueForm13Total + revenueForm19Total + revenueForm10Total;
            reportParams.SimpleReportParams["Сумма35"] = revenueForm5Total;
            reportParams.SimpleReportParams["Сумма7"] = appealCitsForInspectionRtTotal;
            reportParams.SimpleReportParams["Сумма8"] = notPlannedExitDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма9"] = countPrescriptionTotal;
            reportParams.SimpleReportParams["Сумма10"] = countProtocolTotal;
            reportParams.SimpleReportParams["Сумма11"] = countActCheckDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма12"] = countPrescriptionTotal + countProtocolTotal;
            reportParams.SimpleReportParams["Сумма13"] = countActRemovalDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма14"] = countProtocol195FirstDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма15"] = countResolutionCourtFirstDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма16"] = sumResolutionFirstTotal > 0 ? sumResolutionFirstTotal / 1000M : 0;
            reportParams.SimpleReportParams["Сумма17"] = countProtocolArticleLawsFirstDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма18"] = countResolutionHousingInspectionFirstDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма19"] = sumResolutionHousingInspectionFirstTotal > 0 ? sumResolutionHousingInspectionFirstTotal / 1000M : 0;
            reportParams.SimpleReportParams["Сумма20"] = removedFromControlDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма21"] = countActRemovalDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма22"] = countProtocol195Dict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма23"] = countResolutionCourtDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма24"] = sumResolutionCourtTotal > 0 ? sumResolutionCourtTotal / 1000M : 0;
            reportParams.SimpleReportParams["Сумма25"] = countProtocolArticleLawsDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма26"] = countResolutionHousingInspectionDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма27"] = sumResolutionHousingInspectionTotal > 0 ? sumResolutionHousingInspectionTotal / 1000M : 0;
            reportParams.SimpleReportParams["Сумма28"] = removedFromControlProtocolDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма29"] = countSuretyResolveDict.Where(x => x.Key != -1).Sum(i => i.Value);
            reportParams.SimpleReportParams["Сумма30"] = takeOnControl1Total + takeOnControl2Total + takeOnControl3Total + takeOnControl4Total;
            reportParams.SimpleReportParams["Сумма31"] = takeOnControl1Total;
            reportParams.SimpleReportParams["Сумма32"] = takeOnControl2Total;
            reportParams.SimpleReportParams["Сумма33"] = takeOnControl3Total;
            reportParams.SimpleReportParams["Сумма34"] = takeOnControl4Total;

            reportParams.SimpleReportParams["год"] = dateEnd.Year;
            reportParams.SimpleReportParams["год1"] = dateEnd.Year - 1;
            reportParams.SimpleReportParams["год2"] = dateEnd.Year - 2;
            reportParams.SimpleReportParams["год3"] = dateEnd.Year - 3;
            reportParams.SimpleReportParams["год4"] = dateEnd.Year - 4;
        }

        private Dictionary<int, ZonalInspectionProxy> GetZonalInspectionDict()
        {
            var servZonalInspectionMunicipality = Container.Resolve<IDomainService<ZonalInspectionMunicipality>>();
            Dictionary<int, ZonalInspectionProxy> dict;
            try
            {
                 dict = servZonalInspectionMunicipality.GetAll()
                                                  .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.Municipality.Id))
                                                  .Select(x => new { ZonalInspectionId = x.ZonalInspection.Id, ZonalInspection = x.ZonalInspection.ZoneName, MunicipalityId = x.Municipality.Id })
                                                  .OrderBy(x => x.ZonalInspection)
                                                  .AsEnumerable()
                                                  .GroupBy(x => x.ZonalInspectionId)
                                                  .ToDictionary(
                                                      x => x.Key,
                                                      x =>
                                                      new ZonalInspectionProxy
                                                          {
                                                              ZonalInspection = x.Select(y => y.ZonalInspection).First(),
                                                              MunicipalityIds = x.Select(y => y.MunicipalityId).Distinct().ToArray()
                                                          });
            }
            finally
            {
               Container.Release(servZonalInspectionMunicipality);
            }

            return dict;
        }

        // 5.8- снято с контроля (решено положительно) 
        private Dictionary<int, int> GetRemovedFromControlPlusProtocol(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            Dictionary<int, int> zonalByMuIdDict)
        {
            var year = dateStart.AddYears(-1).Year;

            // Берем все предписания и протоколы с постановлением у обращений со статусом закрыто
            var close = servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null && x.baseAppeal.AppealCits.DateFrom != null)
                 .Where(x => x.baseAppeal.AppealCits.DateFrom.Value.Year == year)
                 .Where(x => x.baseAppeal.AppealCits.State.Code == "Закрыто")
                 .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseAppeal.Inspection.Id,
                        StateCode = y.baseAppeal.AppealCits.State.Code,
                        Count = Container.Resolve<IDomainService<Prescription>>().GetAll().Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id)
                        + Container.Resolve<IDomainService<DocumentGjiChildren>>().GetAll().Count(x => x.Parent.Inspection.Id == y.baseAppeal.Inspection.Id
                            && x.Parent.TypeDocumentGji == TypeDocumentGji.Protocol && x.Children.TypeDocumentGji == TypeDocumentGji.Resolution),
                        muId = y.appealRealObj.RealityObject.Municipality.Id
                    })
                 .ToList();

            var baseStatementId = close.Select(x => x.BaseStatementId).ToArray();

            // Берем все предписания и протоколы с постановлением у не закрытых обращений
            var inWork = servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appealRealObj.RealityObject.Municipality.Id))
                 .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null && x.baseAppeal.AppealCits.DateFrom != null)
                .Where(x => x.baseAppeal.AppealCits.DateFrom.Value.Year == year)
                .Where(x => x.baseAppeal.AppealCits.State.Code != "Закрыто")
                .Select(y =>
                   new
                   {
                       BaseStatementId = y.baseAppeal.Inspection.Id,
                       StateCode = y.baseAppeal.AppealCits.State.Code,
                       Count = Container.Resolve<IDomainService<ActRemoval>>().GetAll()
                       .Count(x => x.Inspection.Id == y.baseAppeal.Inspection.Id && x.TypeRemoval == YesNoNotSet.Yes)
                       + Container.Resolve<IDomainService<DocumentGjiChildren>>().GetAll().Count(x => x.Parent.Inspection.Id == y.baseAppeal.Inspection.Id
                           && x.Parent.TypeDocumentGji == TypeDocumentGji.Protocol && x.Children.TypeDocumentGji == TypeDocumentGji.Resolution),
                       muId = y.appealRealObj.RealityObject.Municipality.Id
                   })
               .AsEnumerable()
               .Where(x => !baseStatementId.Contains(x.BaseStatementId)) // исключим из них те которые в закрытых Обращениях уже есть 
               .ToList();

            close.AddRange(inWork);
            return close.GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1).ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.Count).Sum());
        }

        private Dictionary<int, Dictionary<int, int>> GetTakeOnControl(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            Dictionary<int, int> zonalByMuIdDict)
        {
            var date = dateStart.AddYears(-4);

            // В поле «Резолюция» значение: проверить с выходом на место или проверить с выходом на место (по согласованию с прокуратурой) 1.1. Нет Распоряжения.
            var takenOnControl = servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                      && x.baseAppeal.AppealCits.DateFrom != null &&
                      x.baseAppeal.AppealCits.DateFrom >= date && (x.baseAppeal.AppealCits.SuretyResolve.Code == "1" || x.baseAppeal.AppealCits.SuretyResolve.Code == "2"))
                  .Where(y => !this.Container.Resolve<IDomainService<DocumentGji>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id && x.TypeDocumentGji == TypeDocumentGji.Disposal))
                  .Select(x => new
                  {
                      AppealCitsId = x.baseAppeal.AppealCits.Id,
                      x.baseAppeal.AppealCits.DateFrom,
                      muId = x.appealRealObj.RealityObject.Municipality.Id
                  })
                  .ToList();

            // Есть Распоряжение. 
            // У проверки есть нарушения у которых не проставлена "Дата факт. исполнения"
            takenOnControl.AddRange(servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null && x.baseAppeal.AppealCits.DateFrom != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= date
                    && (x.baseAppeal.AppealCits.SuretyResolve.Code == "1" || x.baseAppeal.AppealCits.SuretyResolve.Code == "2"))
                .Where(y => this.Container.Resolve<IDomainService<InspectionGjiViol>>().GetAll().Any(x => x.Inspection.Id == y.baseAppeal.Inspection.Id && x.DateFactRemoval == null))
                .Select(x => new
                {
                    AppealCitsId = x.baseAppeal.AppealCits.Id,
                    x.baseAppeal.AppealCits.DateFrom,
                    muId = x.appealRealObj.RealityObject.Municipality.Id
                })
                .ToList());

            // 1.3. Есть распоряжение. И есть Акт, у которого в поле "нарушения выявлено" стоит "не задано"
            takenOnControl.AddRange(servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                             && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= date
                             && (x.baseAppeal.AppealCits.SuretyResolve.Code == "1" || x.baseAppeal.AppealCits.SuretyResolve.Code == "2"))
                .Where(y => this.Container.Resolve<IDomainService<ActCheckRealityObject>>().GetAll()
                .Any(x => x.ActCheck.Inspection.Id == y.baseAppeal.Inspection.Id && x.HaveViolation == YesNoNotSet.NotSet))
                .Select(x => new
                {
                    AppealCitsId = x.baseAppeal.AppealCits.Id,
                    x.baseAppeal.AppealCits.DateFrom,
                    muId = x.appealRealObj.RealityObject.Municipality.Id
                })
                .ToList());

            // Пункт 2 условия В поле «Резолюция» значение: Дать отчет без выхода на место или Перенаправить в подведомственную организацию.
            // Во вкладке «Ответы» нет записей со значением в поле «Номер».
            takenOnControl.AddRange(servBaseStatementAppealCits.GetAll()
                .Join(
                servAppealCitsRealityObject.GetAll(),
                x => x.AppealCits.Id,
                y => y.AppealCits.Id,
                (a, b) => new { baseAppeal = a, appealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseAppeal.AppealCits.ZonalInspection != null
                    && x.baseAppeal.AppealCits.DateFrom != null && x.baseAppeal.AppealCits.DateFrom >= date
                                               && (x.baseAppeal.AppealCits.SuretyResolve.Code == "3" || x.baseAppeal.AppealCits.SuretyResolve.Code == "4"))
                .Where(y => this.Container.Resolve<IDomainService<AppealCitsAnswer>>().GetAll().All(x => x.AppealCits.Id == y.baseAppeal.AppealCits.Id && x.DocumentNumber == null))
                .Select(x => new
                {
                    AppealCitsId = x.baseAppeal.AppealCits.Id,
                    x.baseAppeal.AppealCits.DateFrom,
                    muId = x.appealRealObj.RealityObject.Municipality.Id
                })
                .ToList());

            return takenOnControl.Distinct(x => x.AppealCitsId).GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1 )
                                 .ToDictionary(
                                     x => x.Key,
                                     x =>
                                     x.GroupBy(y => y.DateFrom.Value.Year)
                                      .ToDictionary(y => y.Key, y => y.Count()));
        }

        /// <summary>
        /// 4.8- снято с контроля (решено положительно) 
        /// Количество документов "Предписание", у которых есть зависимые акты обследования, у которых в поле "Наличие нарушений" стоит значение "Нарушения не выявлены"
        /// </summary>
        /// <param name="servBaseStatementAppealCits">
        /// The serv Base Statement Appeal Cits.
        /// </param>
        /// <param name="servAppealCitsRealityObject">
        /// The serv Appeal Cits Reality Object.
        /// </param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns>
        /// </returns>
        private Dictionary<int, int> GetRemovedFromControl(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            Dictionary<int, int> zonalByMuIdDict)
        {
            // Берем все предписания и протоколы с постановлением у обращений со статусом закрыто
            var close = servBaseStatementAppealCits.GetAll()
               .Join(
                   servAppealCitsRealityObject.GetAll(),
                   x => x.AppealCits.Id,
                   y => y.AppealCits.Id,
                   (a, b) => new { baseApp = a, appByRealObj = b })
               .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseApp.AppealCits.ZonalInspection != null && x.baseApp.AppealCits.DateFrom != null)
                 .Where(x => x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd)
                 .Where(x => x.baseApp.AppealCits.State.Code == "Закрыто")
                 .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseApp.Inspection.Id,
                        StateCode = y.baseApp.AppealCits.State.Code,
                        Count = Container.Resolve<IDomainService<Prescription>>().GetAll().Count(x => x.Inspection.Id == y.baseApp.Inspection.Id)
                        + Container.Resolve<IDomainService<DocumentGjiChildren>>().GetAll().Count(x => x.Parent.Inspection.Id == y.baseApp.Inspection.Id
                            && x.Parent.TypeDocumentGji == TypeDocumentGji.Protocol && x.Children.TypeDocumentGji == TypeDocumentGji.Resolution),
                        muId = y.appByRealObj.RealityObject.Municipality.Id
                    })
                 .ToList();

            var baseStatementId = close.Select(x => x.BaseStatementId).ToArray();

            // Берем все предписания и протоколы с постановлением у не закрытых обращений
            var inWork = servBaseStatementAppealCits.GetAll()
               .Join(
                   servAppealCitsRealityObject.GetAll(),
                   x => x.AppealCits.Id,
                   y => y.AppealCits.Id,
                   (a, b) => new { baseApp = a, appByRealObj = b })
               .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseApp.AppealCits.ZonalInspection != null && x.baseApp.AppealCits.DateFrom != null)
                .Where(x => x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd)
                .Where(x => x.baseApp.AppealCits.State.Code != "Закрыто")
                .Select(y =>
                   new
                   {
                       BaseStatementId = y.baseApp.Inspection.Id,
                       StateCode = y.baseApp.AppealCits.State.Code,
                       Count = Container.Resolve<IDomainService<ActRemoval>>().GetAll()
                       .Count(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.TypeRemoval == YesNoNotSet.Yes)
                       + Container.Resolve<IDomainService<DocumentGjiChildren>>().GetAll().Count(x => x.Parent.Inspection.Id == y.baseApp.Inspection.Id
                           && x.Parent.TypeDocumentGji == TypeDocumentGji.Protocol && x.Children.TypeDocumentGji == TypeDocumentGji.Resolution),
                       muId = y.appByRealObj.RealityObject.Municipality.Id
                   })
               .AsEnumerable()
               .Where(x => !baseStatementId.Contains(x.BaseStatementId)) // исключим из них те которые в закрытых Обращениях уже есть
               .ToList();

            close.AddRange(inWork);
            return close.GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1).ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.Count).Sum());
        }

        /// <summary>
        /// 4.2 оформлено протоколов по ст. 19.5
        /// </summary>
        /// <param name="servBaseStatementAppealCits">
        /// </param>
        /// <param name="servAppealCitsRealityObject">
        /// </param>
        /// <param name="servProtocol">
        /// </param>
        /// <returns>
        /// </returns>
        private Dictionary<int, int> GetCountProtocol195First(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            IDomainService<Protocol> servProtocol,
            Dictionary<int, int> zonalByMuIdDict)
        {
            return servBaseStatementAppealCits.GetAll()
               .Join(
                   servAppealCitsRealityObject.GetAll(),
                   x => x.AppealCits.Id,
                   y => y.AppealCits.Id,
                   (a, b) => new { baseApp = a, appByRealObj = b })
               .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseApp.AppealCits.ZonalInspection != null
                    && x.baseApp.AppealCits.DateFrom != null && x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd)
                .Where(y => servProtocol.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseApp.Inspection.Id,
                        CountProtocol = Container.Resolve<IDomainService<ProtocolArticleLaw>>().GetAll()
                        .Count(x => x.Protocol.Inspection.Id == y.baseApp.Inspection.Id
                            && x.Protocol.Stage.Parent.TypeStage == TypeStage.DisposalPrescription
                            && (x.ArticleLaw.Code == "1" || x.ArticleLaw.Code == "7")),
                        muId = y.appByRealObj.RealityObject.Municipality.Id
                    })
                   .AsEnumerable()
                   .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                   .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountProtocol).Sum());
        }

        /// <summary>
        /// 3.3 - составлено актов обследования
        /// </summary>
        /// <param name="servBaseStatementAppealCits"></param>
        /// <param name="servAppealCitsRealityObject"></param>
        /// <param name="servDisposal"></param>
        /// <param name="servActCheck"></param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns></returns>
        private Dictionary<int, int> GetCountActCheck(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            IDomainService<Disposal> servDisposal,
            IDomainService<ActCheck> servActCheck,
            Dictionary<int, int> zonalByMuIdDict)
        {
            return servBaseStatementAppealCits.GetAll()
               .Join(
                   servAppealCitsRealityObject.GetAll(),
                   x => x.AppealCits.Id,
                   y => y.AppealCits.Id,
                   (a, b) => new { baseApp = a, appByRealObj = b })
               .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseApp.AppealCits.ZonalInspection != null
                     && x.baseApp.AppealCits.DateFrom != null && x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd
                     && (x.baseApp.AppealCits.SuretyResolve.Code == "1" || x.baseApp.AppealCits.SuretyResolve.Code == "2"))
                .Where(y => servDisposal.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.KindCheck.Code == TypeCheck.NotPlannedExit))
                .Where(y => servActCheck.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id))
                .Select(y =>
                    new
                    {
                        BaseStatementId = y.baseApp.Inspection.Id,
                        CountActCheck = servActCheck.GetAll().Count(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.Stage.Parent.TypeStage == TypeStage.Disposal),
                        muId = y.appByRealObj.RealityObject.Municipality.Id
                    })
                 .AsEnumerable()
                 .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                 .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountActCheck).Sum());
        }

        /// <summary>
        /// 3.2 оформлено протоколов (первично)
        /// </summary>
        /// <param name="servBaseStatementAppealCits">
        /// </param>
        /// <param name="servAppealCitsRealityObject">
        /// </param>
        /// <param name="servDisposal">
        /// </param>
        /// <param name="servProtocol">
        /// </param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns>
        /// </returns>
        private Dictionary<int, int> GetCountProtocol(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            IDomainService<Disposal> servDisposal,
            IDomainService<Protocol> servProtocol,
            Dictionary<int, int> zonalByMuIdDict)
        {
            return servBaseStatementAppealCits.GetAll()
               .Join(
                   servAppealCitsRealityObject.GetAll(),
                   x => x.AppealCits.Id,
                   y => y.AppealCits.Id,
                   (a, b) => new { baseApp = a, appByRealObj = b })
               .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseApp.AppealCits.ZonalInspection != null
                      && x.baseApp.AppealCits.DateFrom != null && x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd
                                               && (x.baseApp.AppealCits.SuretyResolve.Code == "1" || x.baseApp.AppealCits.SuretyResolve.Code == "2"))
               .Where(y => servDisposal.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.KindCheck.Code == TypeCheck.NotPlannedExit))
               .Where(y => servProtocol.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id))
                .Select(y =>
                new
                {
                    BaseStatementId = y.baseApp.Inspection.Id,
                    CountProtocol = servProtocol.GetAll().Count(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.Stage.Parent.TypeStage == TypeStage.Disposal),
                    muId = y.appByRealObj.RealityObject.Municipality.Id
                })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountProtocol).Sum());
        }

        /// <summary>
        /// 3.1 Выдано предписаний
        /// </summary>
        /// <param name="servBaseStatementAppealCits">
        /// </param>
        /// <param name="servAppealCitsRealityObject">
        /// </param>
        /// <param name="servDisposal">
        /// </param>
        /// <param name="servPrescription">
        /// </param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns>
        /// </returns>
        private Dictionary<int, int> GetCountPrescription(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            IDomainService<Disposal> servDisposal,
            IDomainService<Prescription> servPrescription,
            Dictionary<int, int> zonalByMuIdDict)
        {          
            return servBaseStatementAppealCits.GetAll()
               .Join(
                   servAppealCitsRealityObject.GetAll(),
                   x => x.AppealCits.Id,
                   y => y.AppealCits.Id,
                   (a, b) => new { baseApp = a, appByRealObj = b })
               .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
               .Where(x => x.baseApp.AppealCits.ZonalInspection != null)
               .Where(x => x.baseApp.AppealCits.DateFrom != null && x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd) 
               .Where(y => servDisposal.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.KindCheck != null && x.KindCheck.Code == TypeCheck.NotPlannedExit))
               .Where(y => servPrescription.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id))
               .Select(y => new
               {
                   BaseStatementId = y.baseApp.Inspection.Id,
                   CountPrescription = servPrescription.GetAll().Where(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.Stage.Parent.TypeStage == TypeStage.Disposal).Select(x => x.Id).Distinct().Count(),
                   muId = y.appByRealObj.RealityObject.Municipality.Id
               })
               .AsEnumerable()
               .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
               .ToDictionary(x => x.Key, x => x.Distinct(y => y.BaseStatementId).Select(y => y.CountPrescription).Sum());
        }

        /// <summary>
        /// 3 Рассмотрено с выездом на место
        /// </summary>
        /// <param name="servBaseStatementAppealCits">
        /// </param>
        /// <param name="servAppealCitsRealityObject">
        /// </param>
        /// <param name="servDisposal">
        /// </param>
        /// <param name="servActCheck">
        /// </param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns>
        /// </returns>
        private Dictionary<int, int> GetNotPlannedExit(
            IDomainService<InspectionAppealCits> servBaseStatementAppealCits,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            IDomainService<Disposal> servDisposal,
            IDomainService<ActCheck> servActCheck,
            Dictionary<int, int> zonalByMuIdDict)
        {
            return servBaseStatementAppealCits.GetAll()
                .Join(
                    servAppealCitsRealityObject.GetAll(),
                    x => x.AppealCits.Id,
                    y => y.AppealCits.Id,
                    (a, b) => new { baseApp = a, appByRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, y => this.municipalityIds.Contains(y.appByRealObj.RealityObject.Municipality.Id))
                .Where(x => x.baseApp.AppealCits.DateFrom != null && x.baseApp.AppealCits.DateFrom >= this.dateStart && x.baseApp.AppealCits.DateFrom <= this.dateEnd)
                .Where(x => x.baseApp.AppealCits.ZonalInspection != null && x.baseApp.AppealCits.DateFrom != null)
                .Where(x => x.baseApp.AppealCits.SuretyResolve != null && (x.baseApp.AppealCits.SuretyResolve.Code == "1" || x.baseApp.AppealCits.SuretyResolve.Code == "2"))
                .Where(y => servDisposal.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id && x.KindCheck != null && x.KindCheck.Code == TypeCheck.NotPlannedExit))
                .Where(y => servActCheck.GetAll().Any(x => x.Inspection.Id == y.baseApp.Inspection.Id))
                .Select(x => new { AppealCitsId = x.baseApp.AppealCits.Id, muId = x.appByRealObj.RealityObject.Municipality.Id })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.muId) ? zonalByMuIdDict[x.muId] : -1)
                .ToDictionary(x => x.Key, x => x.Select(y => y.AppealCitsId).Distinct().Count());
        }

        /// <summary>
        /// 2.2 Данные по ГЖИРТ с разбивкой по районам ЗЖИ
        /// </summary>
        /// <param name="zonalInspectionDict"></param>
        /// <param name="servAppealCitsSource"></param>
        /// <param name="servAppealCitsRealityObject"></param>
        /// <returns></returns>
        private Dictionary<int, int> GetAppealCitsForInspRt(Dictionary<int, ZonalInspectionProxy> zonalInspectionDict, IDomainService<AppealCitsSource> servAppealCitsSource, IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject)
        {
            var appealCitsForInspectionRtDict = new Dictionary<int, int>();
            foreach (var zonalInspection in zonalInspectionDict)
            {
                var zonalInspMunicipality = zonalInspection.Value.MunicipalityIds;
                var appealCitsCount = servAppealCitsSource.GetAll()
                     .Where(y => servAppealCitsRealityObject.GetAll()
                                 .Any(x => x.AppealCits.Id == y.AppealCits.Id && zonalInspMunicipality.Contains(x.RealityObject.Municipality.Id)))
                                 .Where(x => x.AppealCits.DateFrom != null && x.AppealCits.DateFrom >= this.dateStart && x.AppealCits.DateFrom <= this.dateEnd)
                                 .Where(x => x.AppealCits.ZonalInspection.ShortName == "ГЖИРТ")
                                 .Select(x => new { AppealCitsId = x.AppealCits.Id })
                                 .AsEnumerable()
                                 .Distinct()
                                 .Count();

                appealCitsForInspectionRtDict.Add(zonalInspection.Key, appealCitsCount);
            }

            return appealCitsForInspectionRtDict;
        }

        /// <summary>
        /// Строка 2.1  Всего поступило обращений с начала года
        /// </summary>
        /// <param name="service"></param>
        /// <param name="servAppealCitsRealityObject"></param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns></returns>
        private Dictionary<int, int> GetAppealCitsForZonalInspDict(
            IDomainService<AppealCits> service,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject,
            Dictionary<int, int> zonalByMuIdDict)
        {
            return service.GetAll()
                .Join(servAppealCitsRealityObject.GetAll(), x => x.Id, y => y.AppealCits.Id, (a, b) => new { AppealCit = a, AppealRealObj = b })
                .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.AppealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.AppealCit.DateFrom >= this.dateStart && x.AppealCit.DateFrom <= this.dateEnd)
                .Where(x => x.AppealCit.ZonalInspection.ShortName != "ГЖИРТ")
                .Select(x => new { AppealCitsId = x.AppealCit.Id, MunicipalityId = x.AppealRealObj.RealityObject.Municipality.Id })
                .AsEnumerable()
                .GroupBy(x => zonalByMuIdDict.ContainsKey(x.MunicipalityId) ? zonalByMuIdDict[x.MunicipalityId] : -1)
                .ToDictionary(x => x.Key, x => x.Select(y => y.AppealCitsId).Distinct().Count());
        }

        /// <summary>
        /// Строки 2.1.1 - 2.1.4
        /// </summary>
        /// <param name="servAppealCitsSource"></param>
        /// <param name="servAppealCitsRealityObject"></param>
        /// <param name="zonalByMuIdDict"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, int>> GetAppealCitsForRevenueForDict(
            IDomainService<AppealCitsSource> servAppealCitsSource,
            IDomainService<AppealCitsRealityObject> servAppealCitsRealityObject)
        {
            var codesRevenueForm = new[] { "9", "1", "12", "13", "19", "10", "5" };

            return servAppealCitsSource.GetAll()
                .Join(
                    servAppealCitsRealityObject.GetAll(),
                    x => x.AppealCits.Id,
                    y => y.AppealCits.Id,
                    (a, b) => new { AppealCitSource = a, appealRealObj = b })
                .Join(
                    this.Container.Resolve<IDomainService<ZonalInspectionMunicipality>>().GetAll(),
                    x => x.appealRealObj.RealityObject.Municipality.Id,
                    y => y.Municipality.Id,
                    (c, d) => new { c.AppealCitSource, c.appealRealObj, ZonalInspectionMunicipality = d })
                .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.appealRealObj.RealityObject.Municipality.Id))
                .Where(x => x.AppealCitSource.AppealCits.DateFrom >= this.dateStart && x.AppealCitSource.AppealCits.DateFrom <= this.dateEnd)
                .Where(x => codesRevenueForm.Contains(x.AppealCitSource.RevenueForm.Code))
                .Where(x => (x.AppealCitSource.AppealCits.ZonalInspection.ShortName != "ГЖИРТ" && x.ZonalInspectionMunicipality.ZonalInspection.ShortName != "ГЖИРТ")
                    || x.ZonalInspectionMunicipality.ZonalInspection.ShortName == "ГЖИРТ")

                .Select(x => new
                {
                    x.AppealCitSource.RevenueForm.Code,
                    AppealCitsId = x.AppealCitSource.AppealCits.Id,
                    zjiId = x.ZonalInspectionMunicipality.ZonalInspection.Id,
                    x.AppealCitSource.AppealCits.DocumentNumber
                })
                .AsEnumerable()
                .GroupBy(x => x.zjiId)
                .ToDictionary(x => x.Key, x => x.GroupBy(y => y.Code).ToDictionary(y => y.Key, y => y.Select(z => z.AppealCitsId).Distinct().Count()));
        }
    }
}
