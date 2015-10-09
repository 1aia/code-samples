namespace Bars.Gkh.Import.Organization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using Bars.B4;
    using Bars.B4.DataAccess;
    using Bars.B4.Modules.FIAS;
    using Bars.B4.Utils;
    using Bars.Gkh.Entities;
    using Bars.Gkh.Enums;
    using Bars.Gkh.Enums.Import;
    using Bars.Gkh.Import;
    using Bars.Gkh.Import.FiasHelper;
    using Bars.GkhExcel;

    using Castle.Windsor;

    public class OrganizationImport : IGkhImport
    {
        #region Properties

        public virtual IWindsorContainer Container { get; set; }

        public string Key
        {
            get { return "OrganizationImport"; }
        }

        public string CodeImport
        {
            get { return "OrganizationImport"; }
        }

        public string Name
        {
            get { return "Импорт организаций"; }
        }

        public string PossibleFileExtensions
        {
            get { return "xls"; }
        }

        public string PermissionName
        {
            get { return "Import.OrganizationImport"; }
        }

        public ISessionProvider SessionProvider { get; set; }

        public IRepository<RealityObject> RealityObjectRepository { get; set; }

        public IRepository<FiasAddress> FiasAddressRepository { get; set; }

        public IRepository<Contragent> ContragentRepository { get; set; }

        public IRepository<ManagingOrganization> ManagingOrganizationRepository { get; set; }

        public IRepository<SupplyResourceOrg> SupplyResourceOrgRepository { get; set; }

        public IRepository<ServiceOrganization> ServiceOrganizationRepository { get; set; }
        
        // УО
        public IRepository<ManagingOrgRealityObject> ManagingOrgRealityObjectRepository { get; set; }

        public IDomainService<ManOrgContractRealityObject> ManOrgContractRealityObjectDomain { get; set; }

        public IRepository<ManOrgContractOwners> ManOrgContractOwnersRepository { get; set; }

        public IRepository<ManOrgJskTsjContract> ManOrgJskTsjContractRepository { get; set; }

        // Коммунальные услуги
        public IRepository<SupplyResourceOrgRealtyObject> SupplyResourceOrgRealtyObjectRepository { get; set; }

        public IRepository<RealityObjectResOrg> RealityObjectResOrgRepository { get; set; }

        // Жилищные услуги
        public IRepository<ServiceOrgRealityObject> ServiceOrgRealityObjectRepository { get; set; }

        public IRepository<ServiceOrgRealityObjectContract> ServiceOrgRealityObjectContractRepository { get; set; }

        public IRepository<ServiceOrgContract> ServiceOrgContractRepository { get; set; }

        

        public ILogImportManager LogManager { get; set; }

        public IFiasHelper IFiasHelper { get; set; }

        #endregion Properties

        #region Dictionaries

        class RealtyObjectInStreet
        {
            public int roId { get; set; }

            public string House { get; set; }

            public string Letter { get; set; }

            public string Housing { get; set; }

            public string Building { get; set; }
        }

        private readonly Dictionary<int, KeyValuePair<bool, string>> logDict = new Dictionary<int,  KeyValuePair<bool, string>>();

        // 3 - уровневневый словарь
        // Список существующих домов сгруппированных по улице
        // => сгруппированных по населенному пункту
        // => сгруппированных по муниципальному образованию (первого уровня)
        private Dictionary<string, Dictionary<string, Dictionary<string, List<RealtyObjectInStreet>>>> realtyObjectsByAddressDict;

        private Dictionary<string, List<RealtyObjectInStreet>> realtyObjectsByKladrCodeDict;

        private Dictionary<int, int> manOrgByContragentIdDict;

        private Dictionary<int, int> supplyResOrgByContragentIdDict;

        private Dictionary<int, int> serviceOrgByContragentIdDict;

        private Dictionary<string, int> organizationFormDict;

        private List<int> existingRealtyObjectIdList;

        private List<int> existingManagingOrganizations;

        private List<int> existingSupplyResOranizationsg;

        private List<int> existingServiceOrganizations;

        private readonly Dictionary<string, int> headersDict = new Dictionary<string, int>();

        private Dictionary<string, List<int>> contragentsDict;

        private Dictionary<string, KeyValuePair<int, string>> municipalitiesDict;

        // Связь дома и Ук
        private Dictionary<int, List<int>> manOrgRo;

        // Договор между ук и домом
        private Dictionary<int, List<int>> manOrgRoContract;

        // Связь дома и поставщика коммунальных услуг
        private Dictionary<int, List<int>> communalOrgRo;

        // Договор между домом и поставщиком коммунальных услуг
        private Dictionary<int, List<int>> communalOrgRoContract;

        // Связь дома и поставщика жилищных услуг
        private Dictionary<int, List<int>> housingOrgRo;

        // Договор между домом и поставщиком жилищных услуг
        private Dictionary<int, List<int>> housingOrgRoContract;

        #endregion Dictionaries

        private ILogImport logImport;

        private CultureInfo culture;

        protected virtual void InitDictionaries()
        {
            var fiasService = this.Container.Resolve<IDomainService<Fias>>().GetAll();

            var realtyObjects = fiasService
                .Join(
                    fiasService,
                    x => x.AOGuid,
                    x => x.ParentGuid,
                    (a, b) => new { parent = a, child = b })
                .Join(
                    this.RealityObjectRepository.GetAll(),
                    x => x.child.AOGuid,
                    y => y.FiasAddress.StreetGuidId,
                    (c, d) => new { c.parent, c.child, realityObject = d })
                .Where(x => x.parent.ActStatus == FiasActualStatusEnum.Actual)
                .Where(x => x.child.ActStatus == FiasActualStatusEnum.Actual)
                .Where(x => x.child.AOLevel == FiasLevelEnum.Street)
                .Select(x => new
                {
                    localityName = x.parent.OffName,
                    localityShortname = x.parent.ShortName,
                    streetName = x.child.OffName,
                    streetShortname = x.child.ShortName,
                    x.realityObject.FiasAddress.House,
                    x.realityObject.FiasAddress.Letter,
                    x.realityObject.FiasAddress.Housing,
                    x.realityObject.FiasAddress.Building,
                    RealityObjectId = x.realityObject.Id,
                    MunicipalityName = x.realityObject.Municipality.Name,
                    x.child.KladrCode
                })
                .ToArray();

            this.existingRealtyObjectIdList = this.RealityObjectRepository.GetAll().Select(x => x.Id).ToList();

            // 3 - уровневневый словарь
            // Список существующих домов сгруппированных по улице
            // => сгруппированных по населенному пункту
            // => сгруппированных по муниципальному образованию (первого уровня)
            this.realtyObjectsByAddressDict = realtyObjects
                .Where(x => !string.IsNullOrWhiteSpace(x.localityName))
                .Where(x => !string.IsNullOrWhiteSpace(x.streetName))
                .GroupBy(x => (x.MunicipalityName ?? string.Empty).Trim().ToLower())
                .ToDictionary(
                    x => x.Key,
                    x => x.GroupBy(z => (z.localityName + " " + (z.localityShortname ?? string.Empty)).Trim().ToLower())
                          .ToDictionary(
                              z => z.Key,
                              z => z.GroupBy(v => (v.streetName + " " + (v.streetShortname ?? string.Empty)).Trim().ToLower())
                                    .ToDictionary(
                                        v => v.Key,
                                        v => v.Select(u => new RealtyObjectInStreet
                                                {
                                                    roId = u.RealityObjectId,
                                                    House = u.House,
                                                    Letter = u.Letter,
                                                    Housing = u.Housing,
                                                    Building = u.Building
                                                })
                                            .ToList())));

            this.realtyObjectsByKladrCodeDict = realtyObjects
                .Where(x => !string.IsNullOrWhiteSpace(x.KladrCode))
                .GroupBy(x => x.KladrCode)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(y => new RealtyObjectInStreet
                            {
                                roId = y.RealityObjectId,
                                House = y.House,
                                Letter = y.Letter,
                                Housing = y.Housing,
                                Building = y.Building
                            })
                          .ToList());

            this.contragentsDict = this.ContragentRepository.GetAll()
                .Where(x => x.ContragentState != ContragentState.Bankrupt)
                .Where(x => x.ContragentState != ContragentState.Liquidated)
                .Select(x => new { x.Id, x.Inn, x.Kpp })
                .AsEnumerable()
                .Select(x => new
                {
                    x.Id,
                    mixedkey = string.Format(
                        "{0}#{1}",
                        (x.Inn ?? string.Empty).Trim(),
                        (x.Kpp ?? string.Empty).Trim()).ToLower()
                })
                .GroupBy(x => x.mixedkey)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());

            this.existingManagingOrganizations = ManagingOrganizationRepository.GetAll().Select(x => x.Id).ToList();
            this.existingServiceOrganizations = ServiceOrganizationRepository.GetAll().Select(x => x.Id).ToList();
            this.existingSupplyResOranizationsg = SupplyResourceOrgRepository.GetAll().Select(x => x.Id).ToList();

            this.manOrgByContragentIdDict = ManagingOrganizationRepository.GetAll()
                .Select(x => new
                    {
                        ContragentId = x.Contragent.Id,
                        x.Id
                    })
                .AsEnumerable()
                .GroupBy(x => x.ContragentId)
                .ToDictionary(x => x.Key, x => x.First().Id);

            this.serviceOrgByContragentIdDict = ServiceOrganizationRepository.GetAll()
                .Select(x => new
                {
                    ContragentId = x.Contragent.Id,
                    x.Id
                })
                .AsEnumerable()
                .GroupBy(x => x.ContragentId)
                .ToDictionary(x => x.Key, x => x.First().Id);

            this.supplyResOrgByContragentIdDict = SupplyResourceOrgRepository.GetAll()
                .Select(x => new
                {
                    ContragentId = x.Contragent.Id,
                    x.Id
                })
                .AsEnumerable()
                .GroupBy(x => x.ContragentId)
                .ToDictionary(x => x.Key, x => x.First().Id);
            
            // Связь дома и Ук
            this.manOrgRo = ManagingOrgRealityObjectRepository.GetAll()
                .Where(x => x.ManagingOrganization != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.ManagingOrganization.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // Договор между ук и домом
            this.manOrgRoContract = ManOrgContractRealityObjectDomain.GetAll()
                .Where(x => x.ManOrgContract.ManagingOrganization != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.ManOrgContract.ManagingOrganization.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // Связь дома и поставщика коммунальных услуг
            this.communalOrgRo = SupplyResourceOrgRealtyObjectRepository.GetAll()
                .Where(x => x.SupplyResourceOrg != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.SupplyResourceOrg.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // Договор между домом и поставщиком коммунальных услуг
            this.communalOrgRoContract = RealityObjectResOrgRepository.GetAll()
                .Where(x => x.ResourceOrg != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.ResourceOrg.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // Связь дома и поставщика жилищных услуг
            this.housingOrgRo = ServiceOrgRealityObjectRepository.GetAll()
                .Where(x => x.ServiceOrg != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.ServiceOrg.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // Договор между домом и поставщиком жилищных услуг
            this.housingOrgRoContract = ServiceOrgRealityObjectContractRepository.GetAll()
                .Where(x => x.ServOrgContract.ServOrg != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.ServOrgContract.ServOrg.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // словарь Организационно-правовых форм
            organizationFormDict = Container.ResolveDomain<OrganizationForm>().GetAll()
                .Select(x => new { Name = x.Name ?? "", x.Id })
                .AsEnumerable()
                .GroupBy(x => x.Name.ToLower())
                .ToDictionary(x => x.Key, x => x.First().Id);

            // словарь Муниципальных образований
            this.municipalitiesDict = Container.ResolveDomain<Municipality>().GetAll()
                .Select(x => new { Name = x.Name ?? "", x.Id, x.FiasId })
                .AsEnumerable()
                .GroupBy(x => x.Name.ToLower())
                .ToDictionary(x => x.Key, x => new KeyValuePair<int, string>(x.First().Id, x.First().FiasId));
        }

        private string InitHeader(GkhExcelCell[] data)
        {
            this.headersDict["ID_DOMA"] = -1;
            this.headersDict["MU"] = -1;
            this.headersDict["TYPE_CITY"] = -1;
            this.headersDict["CITY"] = -1;
            this.headersDict["TYPE_STREET"] = -1;
            this.headersDict["KLADR"] = -1;
            this.headersDict["STREET"] = -1;
            this.headersDict["HOUSE_NUM"] = -1;
            this.headersDict["LITER"] = -1;
            this.headersDict["KORPUS"] = -1;
            this.headersDict["BUILDING"] = -1;

            this.headersDict["ID_COM"] = -1;
            this.headersDict["TYPE_COM"] = -1;
            this.headersDict["NAME_COM"] = -1;
            this.headersDict["INN"] = -1;
            this.headersDict["KPP"] = -1;
            this.headersDict["OGRN"] = -1;
            this.headersDict["DATE_REG"] = -1;
            this.headersDict["TYPE_LAW_FORM"] = -1;
            this.headersDict["MR_COM"] = -1;
            this.headersDict["MU_COM"] = -1;
            this.headersDict["TYPE_CITY_COM"] = -1;
            this.headersDict["CITY_COM"] = -1;
            this.headersDict["TYPE_STREET_COM"] = -1;
            this.headersDict["STREET_COM"] = -1;
            this.headersDict["KLARD_COM"] = -1;
            this.headersDict["HOUSE_NUM_COM"] = -1;
            this.headersDict["LITER_COM"] = -1;
            this.headersDict["KORPUS_COM"] = -1;
            this.headersDict["BUILDING_COM"] = -1;

            this.headersDict["DATE_START_CON"] = -1;
            this.headersDict["TYPE_CON"] = -1;
            this.headersDict["NUM_DOG"] = -1;
            this.headersDict["DATE_DOG"] = -1;

            for (var index = 0; index < data.Length; ++index)
            {
                var header = data[index].Value.ToUpper();
                if (this.headersDict.ContainsKey(header))
                {
                    this.headersDict[header] = index;
                }
            }

            var requiredFields = new List<string> { "TYPE_COM" };

            var absentColumns = requiredFields.Where(x => !this.headersDict.ContainsKey(x) || this.headersDict[x] == -1).ToList();

            if (absentColumns.Any())
            {
                return "Отсутствуют обязательные столбцы: " + string.Join(", ", absentColumns);
            }

            return string.Empty;
        }

        public void InitLog(string fileName)
        {
            this.LogManager = this.Container.Resolve<ILogImportManager>();
            if (this.LogManager == null)
            {
                throw new Exception("Не найдена реализация интерфейса ILogImportManager");
            }

            this.LogManager.FileNameWithoutExtention = fileName;
            this.LogManager.UploadDate = DateTime.Now;

            this.logImport = this.Container.ResolveAll<ILogImport>().FirstOrDefault(x => x.Key == MainLogImportInfo.Key);
            if (this.logImport == null)
            {
                throw new Exception("Не найдена реализация интерфейса ILogImport");
            }

            this.logImport.SetFileName(fileName);
            this.logImport.ImportKey = this.Key;
        }

        private string GetValue(GkhExcelCell[] data, string field)
        {
            var result = string.Empty;

            if (this.headersDict.ContainsKey(field))
            {
                var index = this.headersDict[field];
                if (data.Length > index && index > -1)
                {
                    result = data[index].Value;
                }
            }

            return result.Trim();
        }
        
        ImportResult IGkhImport.Import(BaseParams baseParams)
        {
            this.culture = CultureInfo.CreateSpecificCulture("ru-RU");

            var file = baseParams.Files["FileImport"];
           
            this.InitLog(file.FileName);

            this.InitDictionaries();

            string message = string.Empty;

            this.InTransaction(() => { message = this.ProcessData(file.Data); });

            if (!string.IsNullOrEmpty(message))
            {
                return new ImportResult(StatusImport.CompletedWithError, message);
            }

            //this.InTransaction(this.SaveData);
            
            this.WriteLogs();

            // Намеренно закрываем текущую сессию, иначе при каждом коммите транзакции
            // ранее измененные дома вызывают каскадирование ФИАС
            this.Container.Resolve<ISessionProvider>().CloseCurrentSession();

            this.LogManager.Add(file, this.logImport);
            this.LogManager.Save();

            message += this.LogManager.GetInfo();
            var status = this.LogManager.CountError > 0 ? StatusImport.CompletedWithError : (this.LogManager.CountWarning > 0 ? StatusImport.CompletedWithWarning : StatusImport.CompletedWithoutError);
            return new ImportResult(status, message, string.Empty, this.LogManager.LogFileId);
        }

        private string ProcessData(byte[] fileData)
        {
            using (var excel = this.Container.Resolve<IGkhExcelProvider>("ExcelEngineProvider"))
            {
                if (excel == null)
                {
                    throw new Exception("Не найдена реализация интерфейса IGkhExcelProvider");
                }

                using (var memoryStreamFile = new MemoryStream(fileData))
                {
                    memoryStreamFile.Seek(0, SeekOrigin.Begin);

                    excel.Open(memoryStreamFile);

                    var rows = excel.GetRows(0, 0);

                    var message = this.InitHeader(rows.First());

                    if (!string.IsNullOrEmpty(message))
                    {
                        return message;
                    }

                    for (var i = 1; i < rows.Count; ++i)
                    {
                        var record = this.ProcessLine(rows[i], i + 1);

                        if (record.isValidRecord)
                        {
                            var organizationId = record.OrganizationId;

                            if (organizationId == 0)
                            {
                                var contragentId = this.CreateOrGetContragentId(record);

                                if (contragentId == 0)
                                {
                                    this.AddLog(record.RowNumber, "Не удалось создать контрагента.", false);
                                    continue;
                                }

                                organizationId = this.CreateOrGetOrganizationId(contragentId, record);

                                record.OrganizationId = organizationId;
                            }

                            CreateContractIfNotExist(organizationId, record);

                            this.AddLog(record.RowNumber, "Успешно", true);
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Обработка строки импорта
        /// </summary>
        private Record ProcessLine(GkhExcelCell[] data, int rowNumber)
        {
            var record = new Record { isValidRecord = false, RowNumber = rowNumber };

            if (data.Length <= 1)
            {
                return record;
            }

            var organizationType = this.GetValue(data, "TYPE_COM");
            if (string.IsNullOrEmpty(organizationType))
            {
                this.AddLog(record.RowNumber, "Не задан тип организации.", false);
                return record;
            }
            else
            {
                record.OrganizationType = this.GetOrganizationType(organizationType);

                if (record.OrganizationType == 0)
                {
                    this.AddLog(record.RowNumber, "Неизвестный тип организации: " + organizationType, false);
                    return record;
                }
            }

            record.ImportOrganizationId = this.GetValue(data, "ID_COM").ToInt();

            if (record.ImportOrganizationId > 0)
            {
                record.OrganizationId = this.GetOrganizationId(record.OrganizationType, record.ImportOrganizationId);
            }

            if (record.OrganizationId == 0)
            {
                record.Inn = this.GetValue(data, "INN");
                record.Kpp = this.GetValue(data, "KPP");

                var mixedKey = string.Format("{0}#{1}", record.Inn, record.Kpp);

                var contragentExists = contragentsDict.ContainsKey(mixedKey);

                if (contragentExists)
                {
                    var contragentsCount = contragentsDict[mixedKey].Count;
                    if (contragentsCount > 1)
                    {
                        this.AddLog(record.RowNumber, "Неоднозначная ситуация. Соответствующих данному ИНН/КПП контрагентов найдено: " + contragentsCount, false);
                        return record;
                    }
                }
                else
                {
                    // Контрагента не нашли, собираем инфу для создания контрагента

                    if (!(Utils.Utils.VerifyInn(record.Inn, false) || Utils.Utils.VerifyInn(record.Inn, true)))
                    {
                        this.AddLog(record.RowNumber, "Некорректный ИНН: " + record.Inn, false);
                        return record;
                    }

                    record.Ogrn = this.GetValue(data, "OGRN");

                    if (!(Utils.Utils.VerifyOgrn(record.Ogrn, false) || Utils.Utils.VerifyOgrn(record.Ogrn, true)))
                    {
                        this.AddLog(record.RowNumber, "Некорректный ОГРН: " + record.Ogrn, false);
                        return record;
                    }

                    record.OrganizationName = this.GetValue(data, "NAME_COM");

                    if (string.IsNullOrEmpty(record.OrganizationName))
                    {
                        this.AddLog(record.RowNumber, "Не задано Наименование юр лица.", false);
                        return record;
                    }

                    record.OrgStreetKladrCode = this.GetValue(data, "KLADR_COM");
                    record.OrgMunicipalityName = this.GetValue(data, "MU_COM");

                    if (string.IsNullOrEmpty(record.OrgMunicipalityName))
                    {
                        record.OrgMunicipalityName = this.GetValue(data, "MR_COM");

                        if (string.IsNullOrEmpty(record.OrgMunicipalityName))
                        {
                            this.AddLog(record.RowNumber, "Не задан мун.район и мун.образование организации.", false);
                            return record;
                        }
                    }

                    if (!this.municipalitiesDict.ContainsKey(record.OrgMunicipalityName.ToLower()))
                    {
                        this.AddLog(record.RowNumber, "Указанный мун.район/мун.образование не найден в системе.", false);
                        return record;
                    }

                    var municipalityProxy = this.municipalitiesDict[record.OrgMunicipalityName.ToLower()];

                    record.ContragentMunicipalityId = municipalityProxy.Key;
                    record.ContragentMunicipalityFiasId = municipalityProxy.Value;

                    record.OrgLocalityName = Simplified(this.GetValue(data, "CITY_COM") + " " + this.GetValue(data, "TYPE_CITY_COM"));
                    record.OrgStreetName = Simplified(this.GetValue(data, "STREET_COM") + " " + this.GetValue(data, "TYPE_STREET_COM"));
                    record.OrgHouse = this.GetValue(data, "HOUSE_NUM_COM");

                    if (string.IsNullOrWhiteSpace(record.OrgHouse))
                    {
                        this.AddLog(record.RowNumber, "Не задан номер дома организации.", false);
                        return record;
                    }

                    record.OrgLetter = this.GetValue(data, "LITER_COM");
                    record.OrgHousing = this.GetValue(data, "KORPUS_COM");
                    record.OrgBuilding = this.GetValue(data, "BUILDING_COM");

                    var organizationForm = this.GetValue(data, "TYPE_LAW_FORM");
                    if (string.IsNullOrEmpty(organizationForm))
                    {
                        this.AddLog(record.RowNumber, "Не задана Организационно-правовая форма.", false);
                        return record;
                    }
                    else
                    {
                        if (!this.organizationFormDict.ContainsKey(organizationForm.ToLower()))
                        {
                            this.AddLog(record.RowNumber, "Неизвестная Организационно-правовая форма: " + organizationForm, false);
                            return record;
                        }

                        record.OrganizationForm = new OrganizationForm { Id = this.organizationFormDict[organizationForm.ToLower()] };
                    }

                    var dateRegistration = this.GetValue(data, "DATE_REG");

                    if (string.IsNullOrWhiteSpace(dateRegistration))
                    {
                        this.AddLog(record.RowNumber, "Не задана Дата регистрации организации.", false);
                        return record;
                    }
                    else
                    {
                        DateTime date;

                        if (DateTime.TryParseExact(dateRegistration, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        {
                            record.DateRegistration = date;
                        }
                        else
                        {
                            this.AddLog(record.RowNumber, "Некорректная дата в поле 'DATE_REG': " + dateRegistration + "'. Дата ожидается в формате 'дд.мм.гггг'", false);
                            return record;
                        }
                    }
                }

                if (record.OrganizationType == OrgType.ManagingOrganization)
                {
                    var managementType = this.GetValue(data, "TYPE_CON");
                    if (string.IsNullOrEmpty(managementType))
                    {
                        this.AddLog(record.RowNumber, "Не задан тип управления.", false);
                        return record;
                    }
                    else
                    {
                        switch (managementType.ToLower())
                        {
                            case "ук":
                                record.TypeManagement = TypeManagementManOrg.UK;
                                break;

                            case "тсж":
                                record.TypeManagement = TypeManagementManOrg.TSJ;
                                break;

                            case "жск":
                                record.TypeManagement = TypeManagementManOrg.JSK;
                                break;

                            default:
                                this.AddLog(
                                    record.RowNumber, "Неизвестный тип управления: " + managementType, false);
                                return record;
                        }
                    }
                }
            }
            
            var accountCreateDate = this.GetValue(data, "DATE_START_CON");

            if (string.IsNullOrWhiteSpace(accountCreateDate))
            {
                this.AddLog(record.RowNumber, "Не задана Дата начала управления/обслуживания домом (а).", false);
                return record;
            }
            else
            {
                DateTime date;

                if (DateTime.TryParseExact(accountCreateDate, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    record.ContractStartDate = date;
                }
                else
                {
                    this.AddLog(record.RowNumber, "Некорректная дата в поле 'DATE_START_CON': " + accountCreateDate + "'. Дата ожидается в формате 'дд.мм.гггг'", false);
                    return record;
                }
            }

            record.DocumentNumber = this.GetValue(data, "NUM_DOG");

            var documentDate = this.GetValue(data, "DATE_DOG");

            if (string.IsNullOrWhiteSpace(documentDate))
            {
                this.AddLog(record.RowNumber, "Не задана Дата договора управления/обслуживания.", false);
                return record;
            }
            else
            {
                DateTime date;

                if (DateTime.TryParseExact(documentDate, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    record.DocumentDate = date;
                }
                else
                {
                    this.AddLog(record.RowNumber, "Некорректная дата в поле 'DATE_DOG': " + accountCreateDate + "'. Дата ожидается в формате 'дд.мм.гггг'", false);
                    return record;
                }
            }
            
            //// 1. Если указан идентификатор дома, пытаемся найти его среди существующих
            record.ImportRealtyObjectId = this.GetValue(data, "ID_DOMA").ToInt();
            if (record.ImportRealtyObjectId > 0 && this.existingRealtyObjectIdList.Contains(record.ImportRealtyObjectId))
            {
                record.RealtyObjectId = record.ImportRealtyObjectId;
            }

            //// 2. Если дом еще не найден, ищем по связке Код КЛАДР + номер дома + литер + корпус + строение
            if (record.RealtyObjectId == 0)
            {
                record.House = this.GetValue(data, "HOUSE_NUM");

                if (string.IsNullOrWhiteSpace(record.House))
                {
                    this.AddLog(record.RowNumber, "Не задан номер дома.", false);
                    return record;
                }

                record.Letter = this.GetValue(data, "LITER");
                record.Housing = this.GetValue(data, "HOUSING");
                record.Building = this.GetValue(data, "BUILDING");
                record.StreetKladrCode = this.GetValue(data, "KLADR");

                if (!string.IsNullOrWhiteSpace(record.StreetKladrCode))
                {
                    var realtyObjectsOnStreet = realtyObjectsByKladrCodeDict[record.StreetKladrCode].Where(x => x.House == record.House).ToList();

                    realtyObjectsOnStreet = string.IsNullOrWhiteSpace(record.Letter)
                        ? realtyObjectsOnStreet.Where(x => string.IsNullOrWhiteSpace(x.Letter)).ToList()
                        : realtyObjectsOnStreet.Where(x => x.Letter == record.Letter).ToList();

                    realtyObjectsOnStreet = string.IsNullOrWhiteSpace(record.Housing)
                        ? realtyObjectsOnStreet.Where(x => string.IsNullOrWhiteSpace(x.Housing)).ToList()
                        : realtyObjectsOnStreet.Where(x => x.Housing == record.Housing).ToList();

                    realtyObjectsOnStreet = string.IsNullOrWhiteSpace(record.Building)
                        ? realtyObjectsOnStreet.Where(x => string.IsNullOrWhiteSpace(x.Building)).ToList()
                        : realtyObjectsOnStreet.Where(x => x.Building == record.Building).ToList();

                    if (realtyObjectsOnStreet.Count > 1)
                    {
                        this.AddLog(record.RowNumber, "Неоднозначный дом. Соответствующих данному набору адресных параметров домов найдено: " + realtyObjectsOnStreet.Count, false);
                        return record;
                    }
                    
                    if (realtyObjectsOnStreet.Count == 1)
                    {
                        record.RealtyObjectId = realtyObjectsOnStreet.First().roId;
                    }
                }
            }

            //// 3. Если дом все еще не найден, ищем по текстовым значениям: МО + Нас.пункт + улица + номер дома + литер + корпус + строение
            if (record.RealtyObjectId == 0)
            {
                record.MunicipalityName = this.GetValue(data, "MU");
                if (string.IsNullOrWhiteSpace(record.MunicipalityName))
                {
                    this.AddLog(record.RowNumber, "Не задано муниципальное образование.", false);
                    return record;
                }

                record.LocalityName = Simplified(this.GetValue(data, "CITY") + " " + this.GetValue(data, "TYPE_CITY"));

                if (string.IsNullOrWhiteSpace(record.LocalityName))
                {
                    this.AddLog(record.RowNumber, "Не задан населенный пункт.", false);
                    return record;
                }

                record.StreetName = Simplified(this.GetValue(data, "STREET") + " " + this.GetValue(data, "TYPE_STREET"));

                if (string.IsNullOrWhiteSpace(record.StreetName))
                {
                    this.AddLog(record.RowNumber, "Не задана улица.", false);
                    return record;
                }
                
                if (!this.realtyObjectsByAddressDict.ContainsKey(record.MunicipalityName.ToLower()))
                {
                    this.AddLog(record.RowNumber, "Не найдены дома в муниципальном образовании: " + record.MunicipalityName, false);
                    return record;
                }

                var municipalityRealtyObjectsDict = this.realtyObjectsByAddressDict[record.MunicipalityName.ToLower()];

                if (!municipalityRealtyObjectsDict.ContainsKey(record.LocalityName.ToLower()))
                {
                    this.AddLog(record.RowNumber, "В указанном МО не найдены дома в населенном пунтке: " + record.LocalityName, false);
                    return record;
                }

                var localityRealtyObjectsDict = municipalityRealtyObjectsDict[record.LocalityName.ToLower()];

                if (!localityRealtyObjectsDict.ContainsKey(record.StreetName.ToLower()))
                {
                    this.AddLog(record.RowNumber, "В указанном населенном пунтк не найдены дома на улице: " + record.StreetName, false);
                    return record;
                }

                var realtyObjectsOnStreet = localityRealtyObjectsDict[record.StreetName.ToLower()].Where(x => x.House == record.House).ToList();

                realtyObjectsOnStreet = string.IsNullOrWhiteSpace(record.Letter)
                    ? realtyObjectsOnStreet.Where(x => string.IsNullOrWhiteSpace(x.Letter)).ToList()
                    : realtyObjectsOnStreet.Where(x => x.Letter == record.Letter).ToList();

                realtyObjectsOnStreet = string.IsNullOrWhiteSpace(record.Housing)
                    ? realtyObjectsOnStreet.Where(x => string.IsNullOrWhiteSpace(x.Housing)).ToList()
                    : realtyObjectsOnStreet.Where(x => x.Housing == record.Housing).ToList();

                realtyObjectsOnStreet = string.IsNullOrWhiteSpace(record.Building)
                    ? realtyObjectsOnStreet.Where(x => string.IsNullOrWhiteSpace(x.Building)).ToList()
                    : realtyObjectsOnStreet.Where(x => x.Building == record.Building).ToList();

                if (realtyObjectsOnStreet.Count == 0)
                {
                    this.AddLog(record.RowNumber, "Дом не найден в системе", false);
                    return record;
                }

                if (realtyObjectsOnStreet.Count > 1)
                {
                    this.AddLog(record.RowNumber, "Неоднозначный дом. Соответствующих данному набору адресных параметров домов найдено: " + realtyObjectsOnStreet.Count, false);
                    return record;
                }

                record.RealtyObjectId = realtyObjectsOnStreet.First().roId;
            }
            
            record.isValidRecord = true;
            
            return record;
        }

        private void WriteLogs()
        {
            foreach (var log in this.logDict.OrderBy(x => x.Key))
            {
                var rowNumber = string.Format("Строка {0}", log.Key);

                if (log.Value.Key)
                {
                    this.logImport.Info(rowNumber, log.Value.Value);
                    this.logImport.CountAddedRows++;
                }
                else
                {
                    this.logImport.Warn(rowNumber, log.Value.Value);
                }
            }
        }

        private void AddLog(int rowNum, string message, bool success)
        {
            if (this.logDict.ContainsKey(rowNum))
            {
                var log = this.logDict[rowNum];

                if (log.Key == success)
                {
                    this.logDict[rowNum] = new KeyValuePair<bool, string>(success, string.Format("{0}; {1}", log.Value, message ?? string.Empty));
                }
                else if (log.Key)
                {
                    this.logDict[rowNum] = new KeyValuePair<bool, string>(success, message ?? string.Empty);
                }
            }
            else
            {
                this.logDict[rowNum] = new KeyValuePair<bool, string>(success, message ?? string.Empty);
            }
        }

        public bool Validate(BaseParams baseParams, out string message)
        {
            message = null;

            var extention = baseParams.Files["FileImport"].Extention;

            var fileExtentions = this.PossibleFileExtensions.Contains(",") ? this.PossibleFileExtensions.Split(',') : new[] { this.PossibleFileExtensions };
            if (fileExtentions.All(x => x != extention))
            {
                message = string.Format("Необходимо выбрать файл с допустимым расширением: {0}", this.PossibleFileExtensions);
                return false;
            }

            return true;
        }

        private int CreateOrGetContragentId(Record record)
        {
            var mixedkey = string.Format("{0}#{1}", record.Inn, record.Kpp).ToLower();
            
            if (this.contragentsDict.ContainsKey(mixedkey))
            {
                return this.contragentsDict[mixedkey].First();
            }

            var fiasAddress = this.CreateAddressForContragent(record);

            if (fiasAddress == null)
            {
                return 0;
            }

            FiasAddressRepository.Save(fiasAddress);
            
            var contragent = new Contragent
            {
                Name = record.OrganizationName,
                Inn = record.Inn,
                Kpp = record.Kpp,
                Ogrn = record.Ogrn,
                FiasJuridicalAddress = fiasAddress,
                JuridicalAddress = fiasAddress.AddressName,
                Municipality = new Municipality { Id = record.ContragentMunicipalityId },
                DateRegistration = record.DateRegistration,
                ContragentState = ContragentState.Active,
                OrganizationForm = record.OrganizationForm
            };

            this.ContragentRepository.Save(contragent);
            this.contragentsDict[mixedkey] = new List<int> { contragent.Id };

            return contragent.Id;
        }
        
        private void InTransaction(Action action)
        {
            using (var transaction = this.Container.Resolve<IDataTransaction>())
            {
                try
                {
                    action();

                    transaction.Commit();
                }
                catch (Exception exc)
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (TransactionRollbackException ex)
                    {
                        throw new DataAccessException(ex.Message, exc);
                    }
                    catch (Exception e)
                    {
                        throw new DataAccessException(
                            string.Format(
                                "Произошла неизвестная ошибка при откате транзакции: \r\nMessage: {0}; \r\nStackTrace:{1};",
                                e.Message,
                                e.StackTrace),
                            exc);
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Returns a string that has space removed from the start and the end, and that has each sequence of internal space replaced with a single space.
        /// </summary>
        /// <param name="initialString"></param>
        /// <returns></returns>
        private static string Simplified(string initialString)
        {
            if (string.IsNullOrEmpty(initialString))
            {
                return initialString;
            }

            var trimmed = initialString.Trim();

            if (!trimmed.Contains(" "))
            {
                return trimmed;
            }

            var result = string.Join(" ", trimmed.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)));

            return result;
        }

        protected virtual OrgType GetOrganizationType(string organizationType)
        {
            // только в реализации модуля 1468
            //case "поставщик ресурсов": return OrgType.ResourceProvider;

            switch (organizationType.ToLower())
            {
                case "ук":                           return OrgType.ManagingOrganization;
                case "поставщик коммунальных услуг": return OrgType.CommunalServiceProvider;
                case "поставщик жилищный услуг":     return OrgType.HousingServiceProvider;
                default:                             return 0;
            }
        }

        private int GetOrganizationId(OrgType organizationType, int importOrganizationId)
        {
            var organizationId = 0;

            switch (organizationType)
            {
                case OrgType.ManagingOrganization:
                    if (this.existingManagingOrganizations.Contains(importOrganizationId))
                    {
                        organizationId = importOrganizationId;
                    }

                    break;

                case OrgType.CommunalServiceProvider:
                    if (this.existingSupplyResOranizationsg.Contains(importOrganizationId))
                    {
                        organizationId = importOrganizationId;
                    }

                    break;

                case OrgType.HousingServiceProvider:
                    if (this.existingServiceOrganizations.Contains(importOrganizationId))
                    {
                        organizationId = importOrganizationId;
                    }

                    break;

                default:
                    organizationId = GetOrganizationIdAdditional(organizationType, importOrganizationId);
                    break;
            }

            return organizationId;
        }

        protected virtual int GetOrganizationIdAdditional(OrgType organizationType, int importOrganizationId)
        {
            return 0;
        }

        private int CreateOrGetOrganizationId(int contragentId, Record record)
        {
            int organizationId;

            switch (record.OrganizationType)
            {
                case OrgType.ManagingOrganization:
                    if (this.manOrgByContragentIdDict.ContainsKey(contragentId))
                    {
                        organizationId = this.manOrgByContragentIdDict[contragentId];
                    }
                    else
                    {
                        // create
                        var managingOrganization = new ManagingOrganization
                            {
                                Contragent = new Contragent { Id = contragentId },
                                TypeManagement = record.TypeManagement,
                                OrgStateRole = OrgStateRole.Active,
                                ActivityGroundsTermination = GroundsTermination.NotSet
                            };

                        ManagingOrganizationRepository.Save(managingOrganization);

                        organizationId = managingOrganization.Id;

                        this.manOrgByContragentIdDict[contragentId] = organizationId;
                    }

                    break;

                case OrgType.CommunalServiceProvider:
                    if (this.supplyResOrgByContragentIdDict.ContainsKey(contragentId))
                    {
                        organizationId = this.supplyResOrgByContragentIdDict[contragentId];
                    }
                    else
                    {
                        // create
                        var supplyResourceOrg = new SupplyResourceOrg
                            {
                                Contragent = new Contragent { Id = contragentId },
                                OrgStateRole = OrgStateRole.Active,
                                ActivityGroundsTermination = GroundsTermination.NotSet
                            };

                        SupplyResourceOrgRepository.Save(supplyResourceOrg);

                        organizationId = supplyResourceOrg.Id;

                        this.supplyResOrgByContragentIdDict[contragentId] = organizationId;
                    }

                    break;

                case OrgType.HousingServiceProvider:
                    if (this.serviceOrgByContragentIdDict.ContainsKey(contragentId))
                    {
                        organizationId = this.serviceOrgByContragentIdDict[contragentId];
                    }
                    else
                    {
                        // create
                        var serviceOrganization = new ServiceOrganization
                        {
                            Contragent = new Contragent { Id = contragentId },
                            OrgStateRole = OrgStateRole.Active,
                            ActivityGroundsTermination = GroundsTermination.NotSet
                        };

                        ServiceOrganizationRepository.Save(serviceOrganization);

                        organizationId = serviceOrganization.Id;

                        this.serviceOrgByContragentIdDict[contragentId] = organizationId;
                    }

                    break;

                default:
                    organizationId = CreateOrGetOrganizationIdAdditional(contragentId, record);
                    break;
            }

            return organizationId;
        }

        protected virtual int CreateOrGetOrganizationIdAdditional(int contragentId, Record record)
        {
            return 0;
        }

        private void CreateContractIfNotExist(int organizationId, Record record)
        {
            // 1. Создать связь между домом и организацией
            // 2. Создать договор между домом и организацией

            switch (record.OrganizationType)
            {
                case OrgType.ManagingOrganization:
                    {
                        // 1
                        var managingOrgRealityObject = new ManagingOrgRealityObject
                            {
                                ManagingOrganization = new ManagingOrganization { Id = organizationId },
                                RealityObject = new RealityObject { Id = record.RealtyObjectId }
                            };
                        
                        if (this.manOrgRo.ContainsKey(organizationId))
                        {
                            var manOrgRobjects = this.manOrgRo[organizationId];
                            if (!manOrgRobjects.Contains(record.RealtyObjectId))
                            {
                                ManagingOrgRealityObjectRepository.Save(managingOrgRealityObject);

                                manOrgRobjects.Add(record.RealtyObjectId);
                            }
                        }
                        else
                        {
                            ManagingOrgRealityObjectRepository.Save(managingOrgRealityObject);

                            this.manOrgRo[organizationId] = new List<int>{ record.RealtyObjectId };
                        }

                        // 2
                        CreateManOrgRoContractIfNotExist(organizationId, record);
                    }

                    break;

                case OrgType.CommunalServiceProvider:
                    {
                        // 1
                        var supplyResourceOrgRealtyObject = new SupplyResourceOrgRealtyObject
                        {
                            SupplyResourceOrg = new SupplyResourceOrg { Id = organizationId },
                            RealityObject = new RealityObject { Id = record.RealtyObjectId }
                        };

                        if (housingOrgRo.ContainsKey(organizationId))
                        {
                            var housingOrgRobjects = housingOrgRo[organizationId];

                            if (!housingOrgRobjects.Contains(record.RealtyObjectId))
                            {
                                SupplyResourceOrgRealtyObjectRepository.Save(supplyResourceOrgRealtyObject);

                                housingOrgRobjects.Add(record.RealtyObjectId);
                            }
                        }
                        else
                        {
                            SupplyResourceOrgRealtyObjectRepository.Save(supplyResourceOrgRealtyObject);

                            housingOrgRo[organizationId] = new List<int> { record.RealtyObjectId };
                        }

                        // 2
                        var realityObjectResOrg = new RealityObjectResOrg
                        {
                            RealityObject = new RealityObject { Id = record.RealtyObjectId },
                            ResourceOrg = new SupplyResourceOrg { Id = organizationId },
                            ContractDate = record.DocumentDate,
                            ContractNumber = record.DocumentNumber,
                            DateStart = record.ContractStartDate
                        };

                        if (housingOrgRoContract.ContainsKey(organizationId))
                        {
                            var housingOrgRoContracts = housingOrgRoContract[organizationId];

                            if (!housingOrgRoContracts.Contains(record.RealtyObjectId))
                            {
                                RealityObjectResOrgRepository.Save(realityObjectResOrg);

                                housingOrgRoContracts.Add(record.RealtyObjectId);
                            }
                        }
                        else
                        {
                            RealityObjectResOrgRepository.Save(realityObjectResOrg);

                            housingOrgRoContract[organizationId] = new List<int> { record.RealtyObjectId };
                        }

                    }

                    break;

                case OrgType.HousingServiceProvider:
                    {
                        // 1

                        var serviceOrgRealityObject = new ServiceOrgRealityObject
                        {
                            ServiceOrg = new ServiceOrganization { Id = organizationId },
                            RealityObject = new RealityObject { Id = record.RealtyObjectId }
                        };

                        if (this.communalOrgRo.ContainsKey(organizationId))
                        {
                            var communalOrgRobjects = this.communalOrgRo[organizationId];
                            if (!communalOrgRobjects.Contains(record.RealtyObjectId))
                            {
                                ServiceOrgRealityObjectRepository.Save(serviceOrgRealityObject);

                                communalOrgRobjects.Add(record.RealtyObjectId);
                            }
                        }
                        else
                        {
                            ServiceOrgRealityObjectRepository.Save(serviceOrgRealityObject);

                            this.communalOrgRo[organizationId] = new List<int> { record.RealtyObjectId };
                        }

                        // 2
                        CreateServiceOrgRealityObjectContractIfNotExist(organizationId, record);
                    }

                    break;

                default:
                    CreateContractIfNotExistAdditional(organizationId, record);
                    break;
            }
        }

        protected virtual void CreateContractIfNotExistAdditional(int organizationId, Record record)
        {

        }

        private void CreateManOrgRoContractIfNotExist(int organizationId, Record record)
        {
            Action createContract = () =>
                {
                    ManOrgBaseContract contract;
                    if (record.TypeManagement == TypeManagementManOrg.UK)
                    {
                        contract = new ManOrgContractOwners
                            {
                                ManagingOrganization = new ManagingOrganization { Id = organizationId },
                                TypeContractManOrgRealObj = TypeContractManOrg.ManagingOrgOwners,
                                StartDate = record.ContractStartDate,
                                DocumentDate = record.DocumentDate,
                                DocumentNumber = record.DocumentNumber
                            };

                        ManOrgContractOwnersRepository.Save(contract);
                    }
                    else
                    {
                        contract = new ManOrgJskTsjContract
                            {
                                ManagingOrganization = new ManagingOrganization { Id = organizationId },
                                TypeContractManOrgRealObj = TypeContractManOrg.JskTsj,
                                StartDate = record.ContractStartDate,
                                DocumentDate = record.DocumentDate,
                                DocumentNumber = record.DocumentNumber
                            };

                        ManOrgJskTsjContractRepository.Save(contract);
                    }
                    
                    var manOrgContractRealityObject = new ManOrgContractRealityObject
                        {
                            ManOrgContract = contract,
                            RealityObject = new RealityObject { Id = record.RealtyObjectId }
                        };

                    ManOrgContractRealityObjectDomain.Save(manOrgContractRealityObject);
                };

            if (this.manOrgRoContract.ContainsKey(organizationId))
            {
                var manOrgContracts = this.manOrgRoContract[organizationId];
                if (!manOrgContracts.Contains(record.RealtyObjectId))
                {
                    createContract();

                    manOrgContracts.Add(record.RealtyObjectId);
                }
            }
            else
            {
                createContract();

                this.manOrgRoContract[organizationId] = new List<int> { record.RealtyObjectId };
            }
        }

        private void CreateServiceOrgRealityObjectContractIfNotExist(int organizationId, Record record)
        {
            Action createContract = () =>
            {
                var contract = new ServiceOrgContract
                {
                    ServOrg = new ServiceOrganization { Id = organizationId },
                    DateStart = record.ContractStartDate,
                    DocumentDate = record.DocumentDate,
                    DocumentNumber = record.DocumentNumber
                };

                ServiceOrgContractRepository.Save(contract);

                var serviceOrgRealityObjectContract = new ServiceOrgRealityObjectContract
                {
                    ServOrgContract = contract,
                    RealityObject = new RealityObject { Id = record.RealtyObjectId }
                };

                ServiceOrgRealityObjectContractRepository.Save(serviceOrgRealityObjectContract);
            };

            if (this.communalOrgRoContract.ContainsKey(organizationId))
            {
                var communalOrgRoContracts = this.communalOrgRoContract[organizationId];
                if (!communalOrgRoContracts.Contains(record.RealtyObjectId))
                {
                    createContract();

                    communalOrgRoContracts.Add(record.RealtyObjectId);
                }
            }
            else
            {
                createContract();

                this.communalOrgRoContract[organizationId] = new List<int> { record.RealtyObjectId };
            }
        }

        private FiasAddress CreateAddressForContragent(Record record)
        {
            var faultReason = string.Empty;
            DynamicAddress address;
            
            if (RecordHasValidCodeKladrStreet(record))
            {
                if (!IFiasHelper.FindInBranchByKladr(record.ContragentMunicipalityFiasId, record.OrgStreetKladrCode, ref faultReason, out address))
                {
                    this.AddLog(record.RowNumber, faultReason, false);
                    return null;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(record.OrgLocalityName))
                {
                    this.AddLog(record.RowNumber, "Не задан населенный пункт организации.", false);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(record.OrgStreetName))
                {
                    this.AddLog(record.RowNumber, "Не задана улица организации.", false);
                    return null;
                }

                if (!IFiasHelper.FindInBranch(record.ContragentMunicipalityFiasId, record.OrgLocalityName, record.OrgStreetName, ref faultReason, out address))
                {
                    this.AddLog(record.RowNumber, faultReason, false);
                    return null;
                }
            }

            return IFiasHelper.CreateFiasAddress(address, record.OrgHouse, record.OrgLetter, record.OrgHousing, record.OrgBuilding); 
        }

        private bool RecordHasValidCodeKladrStreet(Record record)
        {
            if (string.IsNullOrWhiteSpace(record.OrgStreetKladrCode))
            {
                return false;
            }

            var codeLength = record.OrgStreetKladrCode.Length;

            if (codeLength < 15)
            {
                return false;
            }

            if (codeLength > 17)
            {
                record.OrgStreetKladrCode = record.OrgStreetKladrCode.Substring(0, 17);
            }
            else if (codeLength < 17)
            {
                record.OrgStreetKladrCode = record.OrgStreetKladrCode + (codeLength == 15 ? "00" : "0");
            }

            return IFiasHelper.HasValidStreetKladrCode(record.OrgStreetKladrCode);
        }
    }
}