using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Playnite.SDK.Models;

namespace Gumo.Playnite
{
    internal static class GumoMapper
    {
        public static GameMetadata ToGameMetadata(
            GumoGame game,
            IReadOnlyCollection<GumoGameVersion> versions)
        {
            var latestVersion = SelectPreferredVersion(versions);

            return new GameMetadata
            {
                GameId = game.Id,
                Name = game.Name,
                SortingName = EmptyToNull(game.SortingName),
                Description = EmptyToNull(game.Description),
                Platforms = ToMetadataProperties(game.Platforms),
                Genres = ToMetadataProperties(game.Genres),
                Developers = ToMetadataProperties(game.Developers),
                Publishers = ToMetadataProperties(game.Publishers),
                Tags = ToMetadataProperties(game.Tags),
                Links = ToLinks(game.Links),
                CoverImage = ToMetadataFile(game.CoverImage),
                BackgroundImage = ToMetadataFile(game.BackgroundImage),
                Icon = ToMetadataFile(game.Icon),
                ReleaseDate = ParseReleaseDate(game.ReleaseDate),
                Version = BuildVersionLabel(latestVersion),
            };
        }

        public static GumoPatchGameRequest ToPatchGameRequest(
            Game game,
            string coverImage,
            string backgroundImage,
            string icon)
        {
            return new GumoPatchGameRequest
            {
                Name = game.Name,
                SortingName = EmptyToNull(game.SortingName),
                Description = EmptyToNull(game.Description),
                ReleaseDate = SerializeReleaseDate(game.ReleaseDate),
                Genres = ToNames(game.Genres),
                Developers = ToNames(game.Developers),
                Publishers = ToNames(game.Publishers),
                Tags = ToNames(game.Tags),
                Links = ToPatchLinks(game.Links),
                CoverImage = EmptyToNull(coverImage),
                BackgroundImage = EmptyToNull(backgroundImage),
                Icon = EmptyToNull(icon),
            };
        }

        public static Game ToDatabaseGame(
            GumoGame game,
            IReadOnlyCollection<GumoGameVersion> versions,
            Guid pluginId)
        {
            var metadata = ToGameMetadata(game, versions);
            var databaseGame = new Game(metadata.Name)
            {
                PluginId = pluginId,
                GameId = game.Id,
                Name = metadata.Name,
                SortingName = metadata.SortingName,
                Description = metadata.Description,
                ReleaseDate = metadata.ReleaseDate,
                Version = metadata.Version,
                CoverImage = metadata.CoverImage?.Path,
                BackgroundImage = metadata.BackgroundImage?.Path,
                Icon = metadata.Icon?.Path,
                Links = new ObservableCollection<Link>(metadata.Links ?? new List<Link>()),
            };

            foreach (var tag in (metadata.Tags ?? new HashSet<MetadataProperty>()).OfType<MetadataNameProperty>())
            {
                databaseGame.Tags.Add(new Tag(tag.Name));
            }

            return databaseGame;
        }

        private static GumoGameVersion SelectPreferredVersion(IReadOnlyCollection<GumoGameVersion> versions)
        {
            return versions?
                .OrderByDescending(version => version.IsLatest)
                .ThenByDescending(version => version.ReleaseDate)
                .ThenByDescending(version => version.UpdatedAt)
                .FirstOrDefault();
        }

        private static HashSet<MetadataProperty> ToMetadataProperties(IEnumerable<string> values)
        {
            return new HashSet<MetadataProperty>(
                (values ?? Enumerable.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => (MetadataProperty)new MetadataNameProperty(value.Trim())));
        }

        private static List<Link> ToLinks(IEnumerable<GumoLink> links)
        {
            return (links ?? Enumerable.Empty<GumoLink>())
                .Where(link =>
                    link != null &&
                    !string.IsNullOrWhiteSpace(link.Name) &&
                    !string.IsNullOrWhiteSpace(link.Url))
                .Select(link => new Link(link.Name.Trim(), link.Url.Trim()))
                .ToList();
        }

        private static MetadataFile ToMetadataFile(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? null : new MetadataFile(url.Trim());
        }

        private static ReleaseDate? ParseReleaseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
            {
                return new ReleaseDate(dateTime);
            }

            if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out dateTime))
            {
                return new ReleaseDate(dateTime);
            }

            return null;
        }

        private static string BuildVersionLabel(GumoGameVersion version)
        {
            if (version == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(version.VersionCode))
            {
                return $"{version.VersionName} ({version.VersionCode})";
            }

            return EmptyToNull(version.VersionName);
        }

        private static string SerializeReleaseDate(ReleaseDate? releaseDate)
        {
            if (!releaseDate.HasValue)
            {
                return null;
            }

            return releaseDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static List<string> ToNames(IEnumerable<Genre> values)
        {
            return (values ?? Enumerable.Empty<Genre>())
                .Select(value => value?.Name)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private static List<string> ToNames(IEnumerable<Company> values)
        {
            return (values ?? Enumerable.Empty<Company>())
                .Select(value => value?.Name)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private static List<string> ToNames(IEnumerable<Tag> values)
        {
            return (values ?? Enumerable.Empty<Tag>())
                .Select(value => value?.Name)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private static List<GumoLink> ToPatchLinks(IEnumerable<Link> links)
        {
            return (links ?? Enumerable.Empty<Link>())
                .Where(link =>
                    link != null &&
                    !string.IsNullOrWhiteSpace(link.Name) &&
                    !string.IsNullOrWhiteSpace(link.Url))
                .Select(link => new GumoLink
                {
                    Name = link.Name.Trim(),
                    Url = link.Url.Trim(),
                })
                .ToList();
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
