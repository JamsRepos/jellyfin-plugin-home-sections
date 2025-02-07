using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections
{
    /// <summary>
    /// Latest Shows Section.
    /// </summary>
    public class RecentlyAddedShowsSection : IHomeScreenSection
    {
        /// <inheritdoc/>
        public string? Section => "RecentlyAddedShows";

        /// <inheritdoc/>
        public string? DisplayText { get; set; } = "Recently Added Shows";

        /// <inheritdoc/>
        public int? Limit => 1;

        /// <inheritdoc/>
        public string? Route => "tvshows";

        /// <inheritdoc/>
        public string? AdditionalData { get; set; } = "tvshows";

        public object? OriginalPayload { get; set; } = null;
        
        private readonly IUserViewManager m_userViewManager;
        private readonly IUserManager m_userManager;
        private readonly ILibraryManager m_libraryManager;
        private readonly IDtoService m_dtoService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="userViewManager">Instance of <see href="IUserViewManager" /> interface.</param>
        /// <param name="userManager">Instance of <see href="IUserManager" /> interface.</param>
        /// <param name="dtoService">Instance of <see href="IDtoService" /> interface.</param>
        public RecentlyAddedShowsSection(IUserViewManager userViewManager,
            IUserManager userManager,
            ILibraryManager libraryManager,
            IDtoService dtoService)
        {
            m_userViewManager = userViewManager;
            m_userManager = userManager;
            m_libraryManager = libraryManager;
            m_dtoService = dtoService;
        }

        /// <inheritdoc/>
        public QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload)
        {
            User? user = m_userManager.GetUserById(payload.UserId);

            DtoOptions? dtoOptions = new DtoOptions
            {
                Fields = new List<ItemFields>
                {
                    ItemFields.PrimaryImageAspectRatio,
                    ItemFields.Path
                }
            };

            dtoOptions.ImageTypeLimit = 1;
            dtoOptions.ImageTypes = new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop,
                ImageType.Thumb
            };

            MyMediaSection myMedia = new MyMediaSection(m_userViewManager, m_userManager, m_dtoService);
            QueryResult<BaseItemDto> media = myMedia.GetResults(payload);

            Guid parentId = Guid.Empty;
            if (Enum.TryParse(payload.AdditionalData, out CollectionType collectionType))
            {
                parentId = media.Items.FirstOrDefault(x => x.CollectionType == collectionType)?.Id ?? Guid.Empty;
            }
            
            List<Tuple<BaseItem, List<BaseItem>>>? list = m_userViewManager.GetLatestItems(
                new LatestItemsQuery
                {
                    GroupItems = true,
                    Limit = 16,
                    ParentId = parentId,
                    User = user,
                    IsPlayed = false,
                    IncludeItemTypes = Array.Empty<BaseItemKind>()
                },
                dtoOptions);

            IEnumerable<BaseItemDto>? dtos = list.Select(i =>
            {
                BaseItem? item = i.Item2[0];
                int childCount = 0;

                if (i.Item1 != null && (i.Item2.Count > 1 || i.Item1 is MusicAlbum))
                {
                    item = i.Item1;
                    childCount = i.Item2.Count;
                }

                BaseItemDto? dto = m_dtoService.GetBaseItemDto(item, dtoOptions, user);

                dto.ChildCount = childCount;

                return dto;
            });

            return new QueryResult<BaseItemDto>(dtos.ToList());
        }

        /// <inheritdoc/>
        public IHomeScreenSection CreateInstance(Guid? userId, IEnumerable<IHomeScreenSection>? otherInstances = null)
        {
            User? user = m_userManager.GetUserById(userId ?? Guid.Empty);

            Folder? folder = m_libraryManager.GetUserRootFolder()
                .GetChildren(user, true)
                .OfType<Folder>()
                .Select(x => x as ICollectionFolder)
                .Where(x => x != null)
                .FirstOrDefault(x => x.CollectionType == CollectionType.tvshows) as Folder;

            BaseItemDto? originalPayload = null;
            if (folder != null)
            {
                DtoOptions dtoOptions = new DtoOptions();
                dtoOptions.Fields =
                    [..dtoOptions.Fields, ItemFields.PrimaryImageAspectRatio, ItemFields.DisplayPreferencesId];

                originalPayload = Array.ConvertAll(new[] { folder }, i => m_dtoService.GetBaseItemDto(i, dtoOptions, user)).First();
            }

            return new RecentlyAddedShowsSection(m_userViewManager, m_userManager, m_libraryManager, m_dtoService)
            {
                AdditionalData = AdditionalData,
                DisplayText = DisplayText,
                OriginalPayload = originalPayload
            };
        }
    }
}
