using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Newtonsoft.Json;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class LocalGuidesRepository: ILocalGuidesRepository
    {
        public LocalGuidesRepository() { }

        public async Task<PaginatedResult<LocalGuideDto>> GetNearbyLocalGuides(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var localGuides = new List<LocalGuideDto>();
            var functionName = "get_nearby_local_guides";
            var pagingFunctionName = "get_nearby_local_guides_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetLocalGuideParams(
                            skip: skip,
                            latitude: latitude,
                            longitude: longitude,
                            currentPage: currentPage,
                            itemsPerPage: itemsPerPage,
                            maxDistanceKm: null,
                            searchTerm: string.IsNullOrEmpty(searchTerm) ? null : searchTerm
                );

                localGuides = JsonConvert.DeserializeObject<List<LocalGuideDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<LocalGuideDto>(localGuides ?? new List<LocalGuideDto>(), pagination!);
        }

        public async Task<PaginatedResult<LocalGuideDto>> GetNearbyLocalGuidesForCurrentLocalGuide(
            Supabase.Client client,
            Guid localGuideId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var localGuides = new List<LocalGuideDto>();
            var functionName = "get_nearby_local_guides_for_guide";
            var pagingFunctionName = "get_nearby_local_guides_for_guide_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetLocalGuideForCurrentLocalGuideParams(
                            localGuideId: localGuideId,
                            skip: skip,
                            latitude: latitude,
                            longitude: longitude,
                            currentPage: currentPage,
                            itemsPerPage: itemsPerPage,
                            maxDistanceKm: null,
                            searchTerm: string.IsNullOrEmpty(searchTerm) ? null : searchTerm
                );

                localGuides = JsonConvert.DeserializeObject<List<LocalGuideDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<LocalGuideDto>(localGuides ?? new List<LocalGuideDto>(), pagination!);
        }
    
        public async Task<LocalGuideDetailsDto> GetLocalGuideDetails(
            Supabase.Client client,
            Guid localGuideId)
        {
            try {
                var localGuide = await client.From<VwLocalGuides>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, localGuideId.ToString())
                                            .Single();
                var userInfoForLocalGuide = await client.From<AlSaqrUser>().Where(x => x.Id == localGuide.UserId).Single();

                return new LocalGuideDetailsDto()
                {
                    Id = localGuide.Id,
                    UserId = localGuide.UserId,
                    Name = localGuide.Name,
                    CitiesHosted = localGuide.CitiesHosted,
                    RegisteredAt = localGuide.RegisteredAt,
                    UserInfo = userInfoForLocalGuide is null ? null : new LocalGuideUserInfoDto
                    {
                        Id = userInfoForLocalGuide.Id,
                        Username = userInfoForLocalGuide.Username,
                        Avatar = userInfoForLocalGuide.Avatar,
                        Email = userInfoForLocalGuide.Email,
                        BannerImage = userInfoForLocalGuide.BannerImage,
                        Bio = userInfoForLocalGuide.Bio,
                        FirstName = userInfoForLocalGuide.FirstName,
                        LastName = userInfoForLocalGuide.LastName,
                        DateOfBirth = userInfoForLocalGuide.DateOfBirth,
                        CountryOfOrigin = userInfoForLocalGuide.CountryOfOrigin,
                        MaritalStatus = userInfoForLocalGuide.MaritalStatus,
                        Religion = userInfoForLocalGuide.Religion,
                        PreferredMadhab = userInfoForLocalGuide.PreferredMadhab,
                        FrequentMasjid = userInfoForLocalGuide.FrequentMasjid,
                        Hobbies = userInfoForLocalGuide.Hobbies,
                        FavoriteQuranReciters = userInfoForLocalGuide.FavoriteQuranReciters,
                        FavoriteIslamicScholars = userInfoForLocalGuide.FavoriteIslamicScholars,
                        IslamicStudyTopics = userInfoForLocalGuide.IslamicStudyTopics,
                        IsVerified = userInfoForLocalGuide.IsVerified,
                        CreatedAt = userInfoForLocalGuide.CreatedAt,
                        UpdatedAt = userInfoForLocalGuide.UpdatedAt
                    }
                };
            } catch(Exception ex) {
                throw ex;
            }
        }

        public async Task<LocalGuides> CreateLocalGuide(
            Supabase.Client client,
            Guid userId,
            CreateLocalGuideForm form,
            List<Guid> cityIds,
            CancellationToken ct)
        {
            var model = new LocalGuides()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = form.Name ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            var insertedLocalGuide = (await client.From<LocalGuides>().Upsert(model, new QueryOptions()
            {
                Returning = QueryOptions.ReturnType.Representation,
            }, ct)).Model;

            await UpsertLocalGuideCities(client, insertedLocalGuide!.Id, cityIds, ct);

            await CreateLocalGuideNotification(
                client,
                userId,
                insertedLocalGuide.Id,
                insertedLocalGuide.Name,
                "Registered as a local guide named {localGuide}",
                "local_guide_created",
                ct);

            return insertedLocalGuide!;
        }

        public async Task<LocalGuides> UpdateLocalGuide(
            Supabase.Client client,
            Guid localGuideId,
            Guid userId,
            UpsertLocalGuideForm form,
            List<Guid> cityIds,
            CancellationToken ct)
        {
            var existing = await client.From<LocalGuides>().Where(lg => lg.Id == localGuideId).Single(ct);
            if (existing == null)
                throw new Exception("Local guide not found");

            // A local guide record may only be edited by the user it belongs to (spec).
            if (existing.UserId != userId)
                throw new UnauthorizedAccessException("You can only update your own local guide record.");

            existing.Name = AssignStringValue(existing.Name, form.Name);

            var updated = (await client.From<LocalGuides>()
                .Where(lg => lg.Id == existing.Id)
                .Upsert(existing, new QueryOptions { Returning = QueryOptions.ReturnType.Representation }, ct)).Model;

            if (cityIds.Count > 0)
            {
                // Replace the local guide's cities with the supplied set.
                await client.From<LocalGuidesCities>().Where(c => c.LocalGuidesId == localGuideId).Delete();
                await UpsertLocalGuideCities(client, localGuideId, cityIds, ct);
            }

            await CreateLocalGuideNotification(
                client,
                userId,
                existing.Id,
                existing.Name,
                "Updated local guide named {localGuide}",
                "local_guide_updated",
                ct);

            return updated!;
        }

        public async Task<Guid> DeleteLocalGuide(
            Supabase.Client client,
            Guid localGuideId,
            Guid userId,
            CancellationToken ct)
        {
            var existing = await client.From<LocalGuides>().Where(lg => lg.Id == localGuideId).Single(ct);
            if (existing == null)
                throw new Exception("Local guide not found");

            // A user may only unregister (delete) their own local guide record (spec).
            if (existing.UserId != userId)
                throw new UnauthorizedAccessException("You can only unregister your own local guide record.");

            // Notify first, while the local guide row (and its name) still exists.
            await CreateLocalGuideNotification(
                client,
                userId,
                localGuideId,
                existing.Name,
                "Unregistered local guide named {localGuide}",
                "local_guide_deleted",
                ct);

            // Remove join rows that reference the local guide before deleting it.
            await client.From<LocalGuidesCities>().Where(c => c.LocalGuidesId == localGuideId).Delete();

            await client.From<LocalGuides>().Where(lg => lg.Id == localGuideId).Delete();

            return localGuideId;
        }

        private async Task UpsertLocalGuideCities(
            Supabase.Client client,
            Guid localGuideId,
            List<Guid> cityIds,
            CancellationToken ct)
        {
            foreach (var cityId in cityIds)
            {
                await client.From<LocalGuidesCities>().Upsert(
                    new LocalGuidesCities()
                    {
                        Id = Guid.NewGuid(),
                        LocalGuidesId = localGuideId,
                        CityId = cityId,
                        CreatedAt = DateTime.UtcNow
                    },
                    null,
                    ct);
            }
        }

        private async Task CreateLocalGuideNotification(
            Supabase.Client client,
            Guid userId,
            Guid localGuideId,
            string localGuideName,
            string messageTemplate,
            string notificationType,
            CancellationToken ct)
        {
            // The notifications table has no local_guide foreign key, so the local guide is
            // referenced by the acting user and a link only.
            var message = messageTemplate.Replace("{localGuide}", localGuideName);

            var notification = new Notification
            {
                UserId = userId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "local_guide",
                RelatedUserId = userId,
                Link = $"/local-guides/{localGuideId}",
            };

            var created = await client
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = QueryOptions.ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
