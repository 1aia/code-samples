using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Conventions;
using Conventions.Enums;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DataAccess.AzureStorage
{
    public abstract class BaseStorage
    {
        private static readonly Dictionary<MediaObjectType, string> StorageContainerType;

        static BaseStorage()
        {
            StorageContainerType = new Dictionary<MediaObjectType, string>();

            StorageContainerType[MediaObjectType.Image] = "images";
            StorageContainerType[MediaObjectType.Audio] = "audio";
            StorageContainerType[MediaObjectType.Video] = "video";
        }

        public static string GetContainerName(MediaObjectType type)
        {
            return StorageContainerType.ContainsKey(type) ? StorageContainerType[type] : string.Empty;
        }

        private readonly IApplicationSettings _applicationSettings;
        private CloudBlobContainer _container;

        protected abstract MediaObjectType ContainerType { get; }

        protected string ContainerName { get { return StorageContainerType[ContainerType]; }
        }

        protected virtual BlobContainerPublicAccessType BlobContainerPublicAccessType
        {
            get
            {
                return BlobContainerPublicAccessType.Blob;
            }
        }

        public BaseStorage(IApplicationSettings applicationSettings)
        {
            _applicationSettings = applicationSettings;
        }

        protected CloudBlobContainer Container
        {
            get { return _container ?? (_container = GetContainer()); }
        }

        private CloudBlobContainer GetContainer()
        {
            // Retrieve storage account from connection string.
            var storageAccount = CloudStorageAccount.Parse(_applicationSettings.StorageConnectionString);

            // Create the blob client.
            var blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            var container = blobClient.GetContainerReference(ContainerName);

            container.CreateIfNotExists(BlobContainerPublicAccessType);

            return container;
        }

        public void Put(IMediaObjectDataModel mediaObjectModel)
        {
            //https://azure.microsoft.com/ru-ru/documentation/articles/storage-dotnet-how-to-use-blobs/
            //Правила именования больших двоичных объектов

            //Имя большого двоичного объекта может содержать знаки в любом сочетании.
            //Имя большого двоичного объекта должно содержать не менее одного знака и не более 1024 знаков.
            //В именах больших двоичных объектов учитывается регистр.
            //Знаки зарезервированного URL - адреса необходимо должным образом экранировать.
            //Не должно быть более 254 сегментов пути, включающих в себя имя большого двоичного объекта.
            //  Сегмент пути — это строка между последовательными разделителями(например, косая черта «/»), соответствующая имени виртуального каталога.
            //Служба BLOB-объектов основана на схеме неструктурированного хранилища.
            //Вы можете создать виртуальную иерархию, указав знак или разделитель строк в имени большого двоичного объекта.
            //Например, в следующем списке приведены некоторые допустимые и уникальные имена больших двоичных объектов:

            /// a
            /// a.txt
            /// a / b
            /// a / b.txt
            //Чтобы указать большие двоичные объекты иерархически, можно использовать разделитель.

            // Retrieve reference to a blob named "myblob".
            var blockBlob = Container.GetBlockBlobReference(mediaObjectModel.Id.ToString());

            if (string.IsNullOrWhiteSpace(mediaObjectModel.Path))
            {
                var bytes = Convert.FromBase64String(mediaObjectModel.Data);

                blockBlob.UploadFromByteArray(bytes, 0, bytes.Length);
            }
            else
            {
                blockBlob.UploadFromFile(mediaObjectModel.Path, FileMode.Open);
            }
        }

        public void Delete(IMediaObjectModel mediaObjectModel)
        {
            var blockBlob = Container.GetBlobReference(mediaObjectModel.Id.ToString());

            blockBlob.Delete();
        }

        public List<string> GetList()
        {
            var blobs = Container.ListBlobs().ToList();
            var res = blobs.Select(x => x.StorageUri.PrimaryUri.AbsoluteUri).ToList();
            return res;
        }
    }
}
