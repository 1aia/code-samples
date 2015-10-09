using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bars.GkhCr.Report
{
    using Bars.B4;
    using Bars.B4.Modules.ReportPanel.PrintForm;
    using Bars.B4.Reports;
    using Bars.B4.Utils;
    using Bars.Gkh.Entities;
    using Bars.Gkh.Enums;
    using Bars.GkhCr.Entities;
    using System.Linq;

    using Bars.GkhCr.Enums;

    using Castle.Windsor;

    class CrAggregatedReport : BasePrintForm
    {
        public IWindsorContainer Container { get; set; }

        // идентификатор программы КР
        private int programCrId;
        private int[] municipalityIds;
        private int[] finSourceIds;

        public CrAggregatedReport()
            : base(new ReportTemplateBinary(Properties.Resources.CrAggregatedReport))
        {
        }

        public override string Name
        {
            get
            {
                return "Свод_2";
            }
        }

        public override string Desciption
        {
            get
            {
                return "Свод_2";
            }
        }

        public override string GroupName
        {
            get
            {
                return "Формы программы";
            }
        }

        public override string ParamsController
        {
            get
            {
                return "B4.controller.report.CrAggregatedReport";
            }
        }

        public override string RequiredPermission
        {
            get
            {
                return "Reports.CR.CrAggregatedReport";
            }
        }

        public override void SetUserParams(BaseParams baseParams)
        {
            this.programCrId = baseParams.Params["programCrId"].ToInt();
            
            var municipalityIdsList = baseParams.Params.ContainsKey("municipalityIds")
                                  ? baseParams.Params["municipalityIds"].ToString()
                                  : string.Empty;

            this.municipalityIds = !string.IsNullOrEmpty(municipalityIdsList) ? municipalityIdsList.Split(',').Select(id => id.ToInt()).ToArray() : new int[0];

            var finSourceIdsList = baseParams.Params.ContainsKey("finSourceIds")
                      ? baseParams.Params["finSourceIds"].ToString()
                      : string.Empty;

            this.finSourceIds = !string.IsNullOrEmpty(finSourceIdsList) ? finSourceIdsList.Split(',').Select(id => id.ToInt()).ToArray() : new int[0];
        }

        public override void PrepareReport(ReportParams reportParams)
        {
            var serviceTypeWorkCr = Container.Resolve<IDomainService<TypeWorkCr>>();
            var serviceFinanceSourceResource = Container.Resolve<IDomainService<FinanceSourceResource>>();
            var serviceProtocolCr = Container.Resolve<IDomainService<ProtocolCr>>();
            var servicePerformedWorkAct = Container.Resolve<IDomainService<PerformedWorkAct>>();
            
            //Вывод в отчете сегодняшней даты
            reportParams.SimpleReportParams["ReportDate"] = DateTime.Today.ToShortDateString();

            //Query запрос по FinanceSourceResource
            var financeSourceResourceQuery = serviceFinanceSourceResource.GetAll()
                .Where(x => x.ObjectCr.ProgramCr.Id == programCrId)
                .WhereIf(this.finSourceIds.Length > 0, x => this.finSourceIds.Contains(x.FinanceSource.Id))
                .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.ObjectCr.RealityObject.Municipality.Id));

            //Словарь источника финансирования МО по домам
            var financeSourceResourceDict = financeSourceResourceQuery
                .Select(x => new
                        {
                            muName = x.ObjectCr.RealityObject.Municipality.Name,
                            x.ObjectCr.RealityObject.FiasAddress.PlaceName,
                            roId = x.ObjectCr.RealityObject.Id,
                            x.ObjectCr.RealityObject.FiasAddress.StreetName,
                            x.ObjectCr.RealityObject.FiasAddress.House,
                            x.ObjectCr.RealityObject.FiasAddress.Housing,
                            AreaMkd = x.ObjectCr.RealityObject.AreaMkd.HasValue ? x.ObjectCr.RealityObject.AreaMkd.Value : 0,
                            NumberLiving = x.ObjectCr.RealityObject.NumberLiving.HasValue ? x.ObjectCr.RealityObject.NumberLiving.Value : 0
                        })
                .ToList()
                .GroupBy(x => x.muName)
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => 
                    x.GroupBy(y => y.roId)
                    .ToDictionary(y => y.Key, y => y.First()));

            //Словарь  Дом - сумма по актам выполненных работ
            var PerformedWorkActByRoIdDict = servicePerformedWorkAct.GetAll()
                .Where(x => x.ObjectCr.ProgramCr.Id == programCrId)
                .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.ObjectCr.RealityObject.Municipality.Id))
                .Where(x => x.TypeWorkCr.Work.TypeWork == TypeWork.Work)
                .Select(x => new
                    {
                        x.Sum,
                        x.ObjectCr.RealityObject.Id
                    })
                .ToList()
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Sum));

            var protocolCrQuery = serviceProtocolCr.GetAll()
                .Where(x => x.ObjectCr.ProgramCr.Id == programCrId)
                .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.ObjectCr.RealityObject.Municipality.Id));

            //Дома, у которых есть акт "Акт ввода в эксплуатацию после капремонта"
            var roIdsByProtocolCrList = protocolCrQuery
                .Where(x => x.TypeDocumentCr == TypeDocumentCr.ActExpluatatinAfterCr)
                .Select(x => x.ObjectCr.RealityObject.Id)
                .ToList();

            //Словарь домов, отсортированных по актам
            var protocolCrByRoIdDict = protocolCrQuery
                .Where(x => x.TypeDocumentCr == TypeDocumentCr.ActExpluatatinAfterCr || x.TypeDocumentCr == TypeDocumentCr.ProtocolCompleteCr)
                .Select(x => new
                    {
                        x.ObjectCr.RealityObject.Id,
                        x.DateFrom,
                        x.TypeDocumentCr
                    })
                .ToList()
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => 
                    x.GroupBy(y => y.TypeDocumentCr)
                    .ToDictionary(y => y.Key, y => y.Select(z => z.DateFrom).First()));
            
            //Словарь рабочих групп от с до 17
            var workGroups = Enumerable.Range(1, 17).Select(x => x.ToStr()).ToDictionary(x => x, x => x);

            //Список рабочих кодов от 1 до 17
            var workCodes = Enumerable.Range(1, 17).Select(x => x.ToStr()).ToList();

            //Словарь инженерных кодов от 1 до 11
            var engeneerWorkCodes = Enumerable.Range(1, 11).Select(x => x.ToStr()).ToList();
            
            //Изменение значений кодов у рабочих групп
            workGroups["3"] = "2";
            workGroups["8"] = "7";
            workGroups["9"] = "7";
            workGroups["10"] = "7";
            workGroups["11"] = "7";
            workGroups["15"] = "14";
            workGroups["17"] = "16";
            workGroups["1018"] = "1018";
            workGroups["1019"] = "1018";

            //Словарь домов из видов работ КР
            var TypeWorkCrByRoIdDict = serviceTypeWorkCr.GetAll()
                .Where(x => x.ObjectCr.ProgramCr.Id == programCrId)
                .WhereIf(this.finSourceIds.Length > 0, x => this.finSourceIds.Contains(x.FinanceSource.Id))
                .WhereIf(this.municipalityIds.Length > 0, x => this.municipalityIds.Contains(x.ObjectCr.RealityObject.Municipality.Id))
                .Select(x => new
                    {
                        x.Work.Code,
                        roId = x.ObjectCr.RealityObject.Id,
                        PlanSum = x.Sum / 1000,
                        CostSum = x.CostSum / 1000,
                        x.VolumeOfCompletion,
                        x.Volume,
                        x.PercentOfCompletion,
                        AreaMkd = x.ObjectCr.RealityObject.AreaMkd.HasValue ? x.ObjectCr.RealityObject.AreaMkd.Value : 0,
                        NumberLiving = x.ObjectCr.RealityObject.NumberLiving.HasValue ? x.ObjectCr.RealityObject.NumberLiving.Value : 0
                    })
                .ToList()
                .GroupBy(x => x.roId)
                .ToDictionary(
                    x => x.Key, x =>
                        {
                            //Словарь по кодам
                            var workData = x.GroupBy(y => workGroups.ContainsKey(y.Code) ? workGroups[y.Code] : "-1")
                                 .ToDictionary(
                                     y => y.Key,
                                     y => new
                                         {
                                             planSum = y.Sum(z => z.PlanSum.HasValue ? z.PlanSum.Value : 0),
                                             costSum = y.Sum(z => z.CostSum.HasValue ? z.CostSum.Value : 0),
                                             planVolume = y.Sum(z => z.Volume.HasValue ? z.Volume.Value : 0),
                                             volume = y.Sum(z => z.VolumeOfCompletion.HasValue ? z.VolumeOfCompletion.Value : 0)
                                         });

                            //проверка все ли работы завершены у дома
                            var allWorksCompleted = x.All(y => y.PercentOfCompletion == 100);

                            //Плановая сумма по рабочим кодам
                            var allWorksPlanSum = workData.Where(y => workCodes.Contains(y.Key)).Sum(y => y.Value.planSum);

                            //Плановая сумма по инженерным кодам
                            var engeneerWorksPlanSum = workData.Where(y => engeneerWorkCodes.Contains(y.Key)).Sum(y => y.Value.planSum);

                            //Фактическая сумма по рабочим кодам
                            var allWorksSum = workData.Where(y => workCodes.Contains(y.Key)).Sum(y => y.Value.costSum);

                            //Фактическая сумма по инженерным кодам
                            var engeneerWorksSum = workData.Where(y => engeneerWorkCodes.Contains(y.Key)).Sum(y => y.Value.costSum);

                            //Площадь МКД
                            var AreaMkd = x.Select(y => y.AreaMkd).First();

                            //Количество проживающих
                            var NumberLiving = x.Select(y => y.NumberLiving).First();

                            return new { key = x.Key, workData, allWorksCompleted, allWorksSum, engeneerWorksSum, AreaMkd, NumberLiving, allWorksPlanSum, engeneerWorksPlanSum };
                        });

            //Коды по рабочим суммам
            var aggregatedWorkSumCodes = new List<string> { "1", "2", "4", "5", "6", "7", "12", "13", "14", "16", "1018" };

            //Коды по рабочим объемам
            var aggregatedWorkVolumeCodes = new List<string> { "12", "13", "14", "16" };

            //Названия для заполнения шаблона
            var aggregatedDataNames = new List<string> { "roCount", "areaMkd", "numberLiving", "totalEngeneerSum", "totalSum" };
            
            //Словарь по всем домам, у которых завершены все работы
            var aggregatedData = TypeWorkCrByRoIdDict
                .GroupBy(x => x.Value.allWorksCompleted)
                .Select(x =>
                    {
                        var aggregatedDataDict = new Dictionary<string, decimal>();

                        //функции по плановой сумме и объему
                        Func<string, decimal> worksPlanSum = workCode => x.Sum(y => y.Value.workData.ContainsKey(workCode) ? y.Value.workData[workCode].planSum : 0);
                        Func<string, decimal> worksPlanVolume = workCode => x.Sum(y => y.Value.workData.ContainsKey(workCode) ? y.Value.workData[workCode].planVolume / 1000 : 0);

                        //функции по фактической сумме и объему
                        Func<string, List<int>, decimal> worksSum = (workCode, ids) => x.Where(y => ids.Contains(y.Value.key)).Sum(y => y.Value.workData.ContainsKey(workCode) ? y.Value.workData[workCode].costSum : 0);
                        Func<string, List<int>, decimal> worksVolume = (workCode, ids) => x.Where(y => ids.Contains(y.Value.key)).Sum(y => y.Value.workData.ContainsKey(workCode) ? y.Value.workData[workCode].volume / 1000 : 0);

                        //список Id домов, у которых есть акт "Акт ввода в эксплуатацию после капремонта"
                        var roIds = x
                            .Where(y => roIdsByProtocolCrList.Contains(y.Value.key))
                            .Select(y => y.Value.key)
                            .ToList();
                        
                        //словарь плановых сумм по рабочим кодам
                        var aggregatedWorkPlanSumDict = aggregatedWorkSumCodes.ToDictionary(y => y, worksPlanSum);

                        //список плановых сумм по инженерным кодам
                        var totalEngeneerPlanSum = new List<string> { "1", "2", "4", "5", "6", "7" }.Select(y => aggregatedWorkPlanSumDict[y]).Sum();

                        //словарь плановых объемов по рабочим кодам
                        var aggregatedWorkPlanVolumeDict = aggregatedWorkVolumeCodes.ToDictionary(y => y, worksPlanVolume);

                        //словарь фактических сумм по рабочим кодам
                        var aggregatedWorkSumDict = aggregatedWorkSumCodes.ToDictionary(y => y, y => worksSum(y, roIds));

                        //словарь фактических объемов по рабочим кодам
                        var aggregatedWorkVolumeDict = aggregatedWorkVolumeCodes.ToDictionary(y => y, y => worksVolume(y, roIds));

                        //список фактических сумм по инженерным кодам
                        var totalEngeneerSum = new List<string> { "1", "2", "4", "5", "6", "7" }.Select(y => aggregatedWorkSumDict[y]).Sum();

                        //данные по домам, у которых есть акт "Акт ввода в эксплуатацию после капремонта"
                        aggregatedDataDict["roCount"] = roIds.Count();
                        aggregatedDataDict["areaMkd"] = x.Where(y => roIds.Contains(y.Value.key)).Sum(y => y.Value.AreaMkd) / 1000;
                        aggregatedDataDict["numberLiving"] = x.Where(y => roIds.Contains(y.Value.key)).Sum(y => y.Value.NumberLiving);
                        aggregatedDataDict["totalEngeneerSum"] = totalEngeneerSum;
                        aggregatedDataDict["totalSum"] = new List<string> { "12", "13", "14", "16" }.Select(y => aggregatedWorkSumDict[y]).Sum() + totalEngeneerSum;

                        //плановые данные по домам
                        aggregatedDataDict["PlanroCount"] = x.Count();
                        aggregatedDataDict["PlanareaMkd"] = x.Sum(y => y.Value.AreaMkd) / 1000;
                        aggregatedDataDict["PlannumberLiving"] = x.Sum(y => y.Value.NumberLiving);
                        aggregatedDataDict["PlantotalEngeneerSum"] = totalEngeneerPlanSum;
                        aggregatedDataDict["PlantotalSum"] = new List<string> { "12", "13", "14", "16" }.Select(y => aggregatedWorkPlanSumDict[y]).Sum() + totalEngeneerPlanSum;

                        return new { x.Key, aggregatedDataDict, aggregatedWorkPlanSumDict, aggregatedWorkSumDict, aggregatedWorkVolumeDict, aggregatedWorkPlanVolumeDict };
                    })
                .ToDictionary(x => x.Key);

            var fieldsName = new List<string>()
                {
                    "AreaMKD",
                    "PeopleCountMKD",
                    "EngSys",
                    "Device",
                    "RepNetElectro",
                    "RepNetTeplo",
                    "RepNetGas",
                    "RepNetWater",
                    "RepNetWaterDone",
                    "RoofArea",
                    "RoofPrice",
                    "LiftCount",
                    "LiftPrice",
                    "BasementArea",
                    "BasementPrice",
                    "FacadeArea",
                    "FacadePrice",
                    "CostOverhaul",
                    "Consumption",
                    "ActDate",
                    "ActSum",
                    "FinishReport"
                };

            var fieldDict = fieldsName.ToDictionary(x => x, x => string.Empty);
            var mufieldDict = fieldsName.ToDictionary(x => "Itogo" + x, x => new decimal());
            var totalfieldDict = fieldsName.ToDictionary(x => "TotalItogo" + x, x => new decimal());
            var count = 1;

            mufieldDict.Remove("ActDate");
            mufieldDict.Remove("FinishReport");

            var sectionMu = reportParams.ComplexReportParams.ДобавитьСекцию("section");
            var sectionRo = sectionMu.ДобавитьСекцию("sectionRo");

            //заполнение таблицы "1.1. Сведения о многоквартирных домах, в которых полностью завершены ремонтные работы"
            Action<decimal, decimal, string, string> fillReport = (a, b, fieldtype, code) =>
                {
                    reportParams.SimpleReportParams["type1_" + fieldtype + code] = a != 0 ? a.ToStr() : "-";
                    reportParams.SimpleReportParams["type2_" + fieldtype + code] = b != 0 ? b.ToStr() : "-";
                    reportParams.SimpleReportParams["type3_" + fieldtype + code] = Math.Abs(a - b) != 0 ? Math.Abs(a - b).ToStr() : "-";
                };

            //Передача сумм по кодам
            aggregatedWorkSumCodes.ForEach(x =>
            {
                var a = aggregatedData.ContainsKey(true) ? aggregatedData[true].aggregatedWorkPlanSumDict[x] : 0m;
                var b = aggregatedData.ContainsKey(true) ? aggregatedData[true].aggregatedWorkSumDict[x] : 0m;
                fillReport(a, b, "workSum_", x);
            });

            //Передача объемов по кодам
            aggregatedWorkVolumeCodes.ForEach(x =>
            {
                var a = aggregatedData.ContainsKey(true) ? aggregatedData[true].aggregatedWorkPlanVolumeDict[x] : 0m;
                var b = aggregatedData.ContainsKey(true) ? aggregatedData[true].aggregatedWorkVolumeDict[x] : 0m;

                if (x == "14")
                {
                    a = a * 1000;
                    b = b * 1000;
                }
                
                fillReport(a, b, "workVolume_", x);
            });

            //заполнение строк 1,2,3,4,19 в таблице 1.1
            aggregatedDataNames.ForEach(x =>
            {
                var a = aggregatedData.ContainsKey(true) ? aggregatedData[true].aggregatedDataDict["Plan" + x] : 0m;
                var b = aggregatedData.ContainsKey(true) ? aggregatedData[true].aggregatedDataDict[x] : 0m;
                fillReport(a, b, string.Empty, x);
            });
                        
            foreach (var municipality in financeSourceResourceDict.Keys)
            {
                sectionMu.ДобавитьСтроку();

                mufieldDict.Keys.ToList().ForEach(x => mufieldDict[x] = 0);
                
                sectionMu["MOName"] = municipality;
                
                foreach (var realtyObject in financeSourceResourceDict[municipality].Values)
                {
                    fieldDict.Keys.ToList().ForEach(x => fieldDict[x] = string.Empty);

                    sectionRo.ДобавитьСтроку();

                    sectionRo["Num"] = count++;
                    sectionRo["LeavPoint"] = realtyObject.PlaceName;
                    sectionRo["StritName"] = realtyObject.StreetName;
                    sectionRo["HouseNum"] = realtyObject.House;
                    sectionRo["Corpus"] = realtyObject.Housing;

                    fieldDict["AreaMKD"] = realtyObject.AreaMkd != 0 ? realtyObject.AreaMkd.ToStr() : "-";
                    fieldDict["PeopleCountMKD"] = realtyObject.NumberLiving != 0 ? realtyObject.NumberLiving.ToStr() : "-";

                    if (TypeWorkCrByRoIdDict.ContainsKey(realtyObject.roId))
                    {
                        var value = TypeWorkCrByRoIdDict[realtyObject.roId];

                        Func<string, string> worksSum = workCode => value.workData.ContainsKey(workCode) && value.workData[workCode].costSum != 0 ? value.workData[workCode].costSum.ToStr() : "-";
                        Func<string, string> worksVol = workCode => value.workData.ContainsKey(workCode) && value.workData[workCode].volume != 0 ? value.workData[workCode].volume.ToStr() : "-";

                        fieldDict["EngSys"] = value.engeneerWorksSum != 0 ? value.engeneerWorksSum.ToStr() : "-";
                        fieldDict["Device"] = worksSum("7");
                        fieldDict["RepNetElectro"] = worksSum("6");
                        fieldDict["RepNetTeplo"] = worksSum("1");

                        fieldDict["RepNetGas"] = worksSum("5");
                        fieldDict["RepNetWater"] = worksSum("2");
                        fieldDict["RepNetWaterDone"] = worksSum("4");

                        fieldDict["RoofArea"] = worksVol("13");
                        fieldDict["RoofPrice"] = worksSum("13");

                        fieldDict["LiftCount"] = worksVol("14");
                        fieldDict["LiftPrice"] = worksSum("14");

                        fieldDict["BasementArea"] = worksVol("12");
                        fieldDict["BasementPrice"] = worksSum("12");

                        fieldDict["FacadeArea"] = worksVol("16");
                        fieldDict["FacadePrice"] = worksSum("16");

                        fieldDict["CostOverhaul"] = value.allWorksSum != 0 ? value.allWorksSum.ToStr() : "-";
                        fieldDict["Consumption"] = worksSum("1018");
                    }

                    fieldDict["ActDate"] = protocolCrByRoIdDict.ContainsKey(realtyObject.roId)
                                                && protocolCrByRoIdDict[realtyObject.roId].ContainsKey(TypeDocumentCr.ActExpluatatinAfterCr)
                                                ? protocolCrByRoIdDict[realtyObject.roId][TypeDocumentCr.ActExpluatatinAfterCr].Value.ToShortDateString()
                                                : "-";

                    fieldDict["ActSum"] = PerformedWorkActByRoIdDict.ContainsKey(realtyObject.roId) ? PerformedWorkActByRoIdDict[realtyObject.roId].Value.ToStr() : "-";

                    fieldDict["FinishReport"] = protocolCrByRoIdDict.ContainsKey(realtyObject.roId)
                                            && protocolCrByRoIdDict[realtyObject.roId].ContainsKey(TypeDocumentCr.ProtocolCompleteCr)
                                            ? protocolCrByRoIdDict[realtyObject.roId][TypeDocumentCr.ProtocolCompleteCr].Value.ToShortDateString()
                                            : "-";

                    foreach (var ro in fieldDict)
                    {
                        sectionRo[ro.Key] = ro.Value;
                        mufieldDict["Itogo" + ro.Key] += ro.Value != "-" ? ro.Value.ToDecimal() : 0;
                    }
                }

                foreach (var mu in mufieldDict)
                {
                    sectionMu[mu.Key] = mu.Value != 0 ? mu.Value.ToStr() : "-";
                    totalfieldDict["Total" + mu.Key] += mu.Value;
                }
            }

            foreach (var total in totalfieldDict)
            {
                reportParams.SimpleReportParams[total.Key] = total.Value != 0 ? total.Value.ToStr() : "-";
            }
        }
    }
}
