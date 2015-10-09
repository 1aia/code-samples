namespace Bars.Gkh1468.Import.Organization
{
    using System.Collections.Generic;
    using System.Linq;

    using Bars.B4.DataAccess;
    using Bars.Gkh.Entities;
    using Bars.Gkh.Enums;
    using Bars.Gkh1468.Entities;
    using Gkh.Import.Organization;

    // В данном модуле импорт организаций расширяется для импорта Поставщиков ресурсов

    public class OrganizationImport : Gkh.Import.Organization.OrganizationImport
    {
        public IRepository<PublicServiceOrg> PublicServiceOrgRepository { get; set; }

        public IRepository<PublicServiceOrgRealtyObject> PublicServiceOrgRealtyObjectRepository { get; set; }

        public IRepository<RealObjPublicServiceOrg> RealObjPublicServiceOrgRepository { get; set; }

        private Dictionary<int, int> publicServiceOrgByContragentIdDict;

        // Связь дома и поставщика ресурсов
        private Dictionary<int, List<int>> publicServiceOrgRo;

        // Договор между домом и поставщиком ресурсов
        private Dictionary<int, List<int>> publicServiceOrgRoContract;

        private List<int> existingPublicServiceOrgs;

        protected override void InitDictionaries()
        {
            base.InitDictionaries();

            this.existingPublicServiceOrgs = PublicServiceOrgRepository.GetAll().Select(x => x.Id).ToList();

            this.publicServiceOrgByContragentIdDict = PublicServiceOrgRepository.GetAll()
                .Select(x => new
                {
                    ContragentId = x.Contragent.Id,
                    x.Id
                })
                .AsEnumerable()
                .GroupBy(x => x.ContragentId)
                .ToDictionary(x => x.Key, x => x.First().Id);

            // Связь дома и поставщика ресурсов
            this.publicServiceOrgRo = PublicServiceOrgRealtyObjectRepository.GetAll()
                .Where(x => x.PublicServiceOrg != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.PublicServiceOrg.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());

            // Договор между домом и поставщиком ресурсов
            this.publicServiceOrgRoContract = RealObjPublicServiceOrgRepository.GetAll()
                .Where(x => x.PublicServiceOrg != null)
                .Where(x => x.RealityObject != null)
                .Select(x => new { orgId = x.PublicServiceOrg.Id, roId = x.RealityObject.Id })
                .AsEnumerable()
                .GroupBy(x => x.orgId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.roId).ToList());
        }

        protected override OrgType GetOrganizationType(string organizationType)
        {
            switch (organizationType.ToLower())
            {
                case "ук":                           return OrgType.ManagingOrganization;
                case "поставщик коммунальных услуг": return OrgType.CommunalServiceProvider;
                case "поставщик жилищный услуг":     return OrgType.HousingServiceProvider;
                case "поставщик ресурсов":           return OrgType.ResourceProvider;
                default: return 0;
            }
        }

        protected override int GetOrganizationIdAdditional(OrgType organizationType, int importOrganizationId)
        {
            if (organizationType == OrgType.ResourceProvider
                && this.existingPublicServiceOrgs.Contains(importOrganizationId))
            {
                return importOrganizationId;
            }
            
            return 0;
        }

        protected override int CreateOrGetOrganizationIdAdditional(int contragentId, Record record)
        {
            if (record.OrganizationType != OrgType.ResourceProvider)
            {
                return 0;
            }

            if (this.publicServiceOrgByContragentIdDict.ContainsKey(contragentId))
            {
                return this.publicServiceOrgByContragentIdDict[contragentId];
            }

            // create
            var publicServiceOrg = new PublicServiceOrg
            {
                Contragent = new Contragent { Id = contragentId },
                OrgStateRole = OrgStateRole.Active,
                ActivityGroundsTermination = GroundsTermination.NotSet
            };

            PublicServiceOrgRepository.Save(publicServiceOrg);


            this.publicServiceOrgByContragentIdDict[contragentId] = publicServiceOrg.Id;

            return publicServiceOrg.Id;
        }

        protected override void CreateContractIfNotExistAdditional(int organizationId, Record record)
        {
            if (record.OrganizationType != OrgType.ResourceProvider)
            {
                return;
            }

            // 1. Создать связь между домом и организацией
            // 2. Создать договор между домом и организацией

            // 1
            var publicServiceOrgRealtyObject = new PublicServiceOrgRealtyObject
            {
                PublicServiceOrg = new PublicServiceOrg { Id = organizationId },
                RealityObject = new RealityObject { Id = record.RealtyObjectId }
            };

            if (publicServiceOrgRo.ContainsKey(organizationId))
            {
                var publicServiceOrgRobjects = publicServiceOrgRo[organizationId];

                if (!publicServiceOrgRobjects.Contains(record.RealtyObjectId))
                {
                    PublicServiceOrgRealtyObjectRepository.Save(publicServiceOrgRealtyObject);

                    publicServiceOrgRobjects.Add(record.RealtyObjectId);
                }
            }
            else
            {
                PublicServiceOrgRealtyObjectRepository.Save(publicServiceOrgRealtyObject);

                publicServiceOrgRo[organizationId] = new List<int> { record.RealtyObjectId };
            }

            // 2
            var realObjPublicServiceOrg = new RealObjPublicServiceOrg
            {
                RealityObject = new RealityObject { Id = record.RealtyObjectId },
                PublicServiceOrg = new PublicServiceOrg { Id = organizationId },
                ContractDate = record.DocumentDate,
                ContractNumber = record.DocumentNumber,
                DateStart = record.ContractStartDate
            };

            if (publicServiceOrgRoContract.ContainsKey(organizationId))
            {
                var publicServiceOrgRoContracts = publicServiceOrgRoContract[organizationId];

                if (!publicServiceOrgRoContracts.Contains(record.RealtyObjectId))
                {
                    RealObjPublicServiceOrgRepository.Save(realObjPublicServiceOrg);

                    publicServiceOrgRoContracts.Add(record.RealtyObjectId);
                }
            }
            else
            {
                RealObjPublicServiceOrgRepository.Save(realObjPublicServiceOrg);

                publicServiceOrgRoContract[organizationId] = new List<int> { record.RealtyObjectId };
            }
        }
    }
}