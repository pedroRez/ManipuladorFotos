using ManipuladorFotos.Models;

namespace ManipuladorFotos.Services;

public sealed class FilterService
{
    public IEnumerable<MediaItem> Apply(IEnumerable<MediaItem> items, FilterCriteria criteria)
    {
        var query = items;

        if (!string.IsNullOrWhiteSpace(criteria.NameContains))
        {
            query = query.Where(x => x.Name.Contains(criteria.NameContains, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.ExtensionContains))
        {
            query = query.Where(x => x.Extension.Contains(criteria.ExtensionContains, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(criteria.TypeFilter, "Todos", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Kind.ToString().Equals(criteria.TypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.CreatedFrom.HasValue)
        {
            query = query.Where(x => x.PrimaryPhotoDate >= criteria.CreatedFrom.Value.Date);
        }

        if (criteria.CreatedTo.HasValue)
        {
            var date = criteria.CreatedTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.PrimaryPhotoDate <= date);
        }

        if (criteria.ModifiedFrom.HasValue)
        {
            query = query.Where(x => x.PrimaryPhotoDate >= criteria.ModifiedFrom.Value.Date);
        }

        if (criteria.ModifiedTo.HasValue)
        {
            var date = criteria.ModifiedTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.PrimaryPhotoDate <= date);
        }

        if (criteria.MinSizeBytes.HasValue)
        {
            query = query.Where(x => x.SizeBytes >= criteria.MinSizeBytes.Value);
        }

        if (criteria.MaxSizeBytes.HasValue)
        {
            query = query.Where(x => x.SizeBytes <= criteria.MaxSizeBytes.Value);
        }

        return query;
    }
}
