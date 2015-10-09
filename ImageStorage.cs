using Conventions;
using Conventions.Enums;

namespace DataAccess.AzureStorage
{
    public class ImageStorage : BaseStorage, IImageStorage
    {
        protected override MediaObjectType ContainerType
        {
            get { return MediaObjectType.Image; }
        }

        public ImageStorage(IApplicationSettings applicationSettings) : base(applicationSettings)
        {
        }
    }
}