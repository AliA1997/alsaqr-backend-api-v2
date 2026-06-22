using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Newtonsoft.Json;
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
    }
}
