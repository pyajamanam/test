using System.Collections.Generic;
using **.Apps.WebSites.Common.Models;
using **.Apps.WebSites.Common.Framework.Helpers;
using **.Library.Common;
using **.Library.DTO;
using **.Library.DTO.DtoType;
using **.Library.DTO.ResultType;
using **.Library.Shared;
using System;
using System.Globalization;
using System.Linq;
using System.Web;
using **.Providers.Product.ClientExtensions;
using Umbraco.Core.Security;
using Cms = umbraco.cms;
using System.Web.Security;
using System.Security.Principal;
using System.Security.Claims;
using **.Apps.WebSites.Helpers;
using **.Apps.WebSites.Models;
using **.Apps.WebSites.Security.DataLayer;

namespace **.Apps.WebSites.Framework.Helpers
{

    public static class SecurityHelper
    {
        #region Consts

        private const Application CurrentApplication = Application.CLIENTPORTAL;

        //private const string LoggedCustomerSession = "KORcvSCsIQ";
        //private const string LoggedDealerSession = "HDgAbAGOcE";
        //private const string LoggedAgentSession = "ChAQIhadjw";

        private const string CustomerNotSet = "Customer not set.";

        #endregion


        #region User

        public static IIdentity LoggedUser
        {
            get { return HttpContext.Current.User.Identity; }
        }

        public static ClaimsIdentity CurrentUser
        {
            get
            {
                return HttpContext.Current.User.Identity as ClaimsIdentity;

            }
        }

        public static long? CurrentUserId
        {
            get
            {
                var currentUser = LoggedUser;
                if (currentUser == null) return null;
                long custId;
                long.TryParse(CurrentUser.FindFirst(ClaimTypes.NameIdentifier).Value, out custId);
                return custId; //return type pk of customMember members db 
            }
        }

        public static string CurrentUserCustomerNumber
        {
            get
            {
                var currentUser = LoggedUser;
                if (currentUser == null) return null;
                if (IsDealerOrAgent) return null;

                var customerNumber = CurrentUser.FindFirst("CustomerNumber");
                if (customerNumber == null || customerNumber.Value == null) return null;

                var value = customerNumber.Value;
                if (string.IsNullOrWhiteSpace(value)) return null;

                return value;
            }
        }

        public static string CurrentUserEmail
        {
            get
            {
                var currentUser = LoggedUser;
                if (currentUser == null) return null;
                if (IsDealerOrAgent) return null;

                var customerEmail = CurrentUser.FindFirst("Email");
                if (customerEmail == null || customerEmail.Value == null) return null;

                var value = customerEmail.Value;
                if (string.IsNullOrWhiteSpace(value)) return null;
                return value;
            }
        }

        public static ValueResult<bool> IsCustomerLocked
        {
            get
            {
                var lockedOut = UmbracoCustomerHelper.GetUserByCustomerNumber(CustomerNumber).IsLockedOut;
                return lockedOut == null
                    ? new ValueResult<bool>(CustomerNotSet, Result.FAILED)
                    : new ValueResult<bool>(Result.SUCCESS, lockedOut.Value);
            }
        }

        public static bool IsDealer
        {
            get
            {
                return CurrentUser.HasClaim(ClaimTypes.Role, CmsUserType.DEALER.ToString());
            }
        }

        public static bool IsAgent
        {
            get
            {
                return CurrentUser.HasClaim(ClaimTypes.Role, CmsUserType.AGENT.ToString());
            }
        }

        public static CmsUserType GetRoleType
        {
            get
            {

                return IsAgent ? CmsUserType.AGENT : IsDealer ? CmsUserType.DEALER : IsWebshopUser ? CmsUserType.WEBSHOPBACKEND : CmsUserType.USER;
                //CurrentUser.HasClaim(ClaimTypes.Role, CmsUserType.AGENT.ToString());
            }
        }

        public static bool IsWebshopUser
        {
            get
            {
                return CurrentUser.HasClaim(ClaimTypes.Role, CmsUserType.WEBSHOPBACKEND.ToString());
            }
        }

        public static bool IsDealerOrAgent
        {
            get
            {
                return CurrentUser.HasClaim(ClaimTypes.Role, CmsUserType.DEALER.ToString()) || CurrentUser.HasClaim(ClaimTypes.Role, CmsUserType.AGENT.ToString());
            }
        }

        public static string GetLoggedAsText()
        {
            if (Agent != null)
                return string.Format("{0} ({1})", Agent.Dealer.CmsFirstName, Agent.Name);

            return LoggedUser == null ? null : LoggedUser.Name;
        }

        #endregion

        public static AgentDTO Agent { get; set; }

        public static DealerDTO Dealer { get; set; }

        public static CustomerDTO Customer { get; set; }

        public static string CustomerNumber
        {
            get
            {
                return Customer != null ? Customer.Id : CurrentUserCustomerNumber;
            }
        }

        public static int CustomerNumberInt
        {
            get
            {
                int custId;
                int.TryParse(CustomerNumber, out custId);
                return custId;
            }
        }

        #region Public methods

        /// <summary>
        /// to convert to DealerDto
        /// </summary>
        /// <param name="memberobj"></param>
        /// <returns></returns>
        public static DealerDTO MergeUserToDealerDto(User memberobj)
        {
            var dto = new DealerDTO()
            {
                CmsId = memberobj.SyncUID,
                CmsFirstName = memberobj.FirstName,
                CmsLastName = memberobj.LastName,
                CmsUserName = memberobj.UserName,
                Postcode = memberobj.ZipCode,
                Email = memberobj.Email,
                Active = (memberobj.IsLockedOut != null &&
                     (memberobj.IsLockedOut == false && memberobj.IsDeleted == false ? true : false)),
                Status = BoolValue.TRUE, // Status = memberobj.IsApproved ? BoolValue.TRUE : BoolValue.FALSE,
                // LastEditDate = memberobj.UpdatedDate.HasValue ? memberobj.UpdatedDate.Value : default(DateTime),
                UserType = CmsUserType.DEALER
                //addtional proerties are there but this will be used on further.
            };
            Dealer = dto;
            Agent = null; //only for agent not to dealer
            return dto;
        }

        /// <summary>
        /// to convert to AgentDto
        /// </summary>
        /// <param name="memberobj"></param>
        /// <param name="dealerobj"></param>
        public static void MergeUserToAgentDto(User memberobj, User dealerobj)
        {
            var dto = new AgentDTO()
            {
                Dealer = string.IsNullOrEmpty(dealerobj.DealerNumber) ? null : MergeUserToDealerDto(dealerobj),
                Id = memberobj.SyncUID,
                Name = memberobj.UserName,
                CmsFirstName = memberobj.UserName, //no other coloumn in db for firstname so take username
                Email = memberobj.Email,
                UserType = CmsUserType.AGENT,
                Status = BoolValue.TRUE, //memberobj.IsApproved ? BoolValue.TRUE : BoolValue.FALSE,
                //addtional proerties are there but this will be used on further.
            };
            Agent = dto;
        }

        public static void ClearclaimsDTOs()
        {
            Agent = null;
            Dealer = null;
            Customer = null;
        }

        #endregion

        public static void UpdateSourceToBasket(bool isAffiliate = false, bool isUpgradeFlow = false)
        {
            //Update source, dealer number, dealer name in basket to display in confirmation pdf
            var basketId = isUpgradeFlow ? BasketHelper.GetUpgradeBasketId() : BasketHelper.GetBasketId();
            var loggedUserData = LoggedUser;
            if (loggedUserData != null && IsDealerOrAgent)
            {
                var source = string.Empty;
                var dealerId = new Guid();
                var createdBy = loggedUserData.Name;
                var createdName = loggedUserData.Name + " " + loggedUserData.Name;

                if (IsDealer)
                {
                    source = "Dealer";
                    var company = Configuration.ConfigurationHelper.**WsConfig.Company;
                    if (company != Company.TVVLAANDEREN && company != Company.TELESAT)
                        dealerId = GetDealerId(createdBy);
                    // This is only required for HDAustria for now. As we are yet to map the dealers to correct groups in TVV/TSAT for campaigns.
                }
                else if (IsAgent)
                {
                    source = "Agent";
                }

                if (dealerId == new Guid())
                {
                    dealerId = BasketHelper.GetDefaultDealerId();
                }

                var updateBasketInfo = new CreateBasketDTO
                {
                    BasketId = basketId,
                    Source = source,
                    CreatedBy = createdBy,
                    CreatedName = (isAffiliate ? "Affiliate-" : string.Empty) + createdName,
                    DealerId = dealerId

                };
                **.ServiceBus.Extern.Basket.GetService().UpdateSourceToBasket(updateBasketInfo);
            }
        }

        public static Guid GetDealerId(string dealerName)
        {
            if (!IsDealerOrAgent)
            {
                return BasketHelper.GetDefaultDealerId();
            }

            //UserName is IBS Number. This should be the external billing id for the dealers in **Standing.
            var dealerInfo = **.ServiceBus.Extern.Dealer.GetService().GetDealerByCmsIdOrName(dealerName);
            //It says CMSID but the code looks for BillingSystemID.
            if (dealerInfo.Result.IsNotSuccess)
            {
                // var message = string.Format("SecurityHelper.GetDealerId - can't find dealer with name: {0}", dealerName);
                return BasketHelper.GetDefaultDealerId();
            }

            return dealerInfo.Id;
        }

        public static string GetClaimName(this IPrincipal user)
        {
            var claim = ((ClaimsIdentity)user.Identity).FindFirst("CustomerNumber");
            return claim == null ? null : claim.Value;
        }
    }
}