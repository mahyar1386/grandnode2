﻿using Grand.Business.Catalog.Interfaces.Discounts;
using Grand.Business.Common.Extensions;
using Grand.Business.Common.Interfaces.Directory;
using Grand.Business.Common.Interfaces.Localization;
using Grand.Business.Common.Interfaces.Seo;
using Grand.Business.Common.Interfaces.Stores;
using Grand.Business.Customers.Interfaces;
using Grand.Business.Customers.Events;
using Grand.Business.Storage.Interfaces;
using Grand.Domain;
using Grand.Domain.Directory;
using Grand.Domain.Discounts;
using Grand.Domain.Seo;
using Grand.Domain.Vendors;
using Grand.SharedKernel.Extensions;
using Grand.Web.Admin.Extensions;
using Grand.Web.Admin.Interfaces;
using Grand.Web.Admin.Models.Customers;
using Grand.Web.Admin.Models.Vendors;
using MediatR;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Admin.Services
{
    public partial class VendorViewModelService : IVendorViewModelService
    {
        private readonly IDiscountService _discountService;
        private readonly IVendorService _vendorService;
        private readonly ICustomerService _customerService;
        private readonly ITranslationService _translationService;
        private readonly IDateTimeService _dateTimeService;
        private readonly ICountryService _countryService;
        private readonly IStoreService _storeService;
        private readonly ISlugService _slugService;
        private readonly IPictureService _pictureService;
        private readonly IMediator _mediator;
        private readonly ILanguageService _languageService;
        private readonly SeoSettings _seoSettings;
        private readonly VendorSettings _vendorSettings;

        public VendorViewModelService(IDiscountService discountService, IVendorService vendorService, ICustomerService customerService, ITranslationService translationService,
            IDateTimeService dateTimeService, ICountryService countryService, IStoreService storeService, ISlugService slugService,
            IPictureService pictureService, IMediator mediator, VendorSettings vendorSettings, ILanguageService languageService,
            SeoSettings seoSettings)
        {
            _discountService = discountService;
            _vendorService = vendorService;
            _customerService = customerService;
            _translationService = translationService;
            _dateTimeService = dateTimeService;
            _countryService = countryService;
            _storeService = storeService;
            _slugService = slugService;
            _pictureService = pictureService;
            _mediator = mediator;
            _languageService = languageService;
            _vendorSettings = vendorSettings;
            _seoSettings = seoSettings;
        }

        public virtual async Task PrepareDiscountModel(VendorModel model, Vendor vendor, bool excludeProperties)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            model.AvailableDiscounts = (await _discountService
                .GetAllDiscounts(DiscountType.AssignedToVendors, showHidden: true))
                .Select(d => d.ToModel(_dateTimeService))
                .ToList();

            if (!excludeProperties && vendor != null)
            {
                model.SelectedDiscountIds = vendor.AppliedDiscounts.ToArray();
            }
        }

        public virtual async Task PrepareVendorReviewModel(VendorReviewModel model,
            VendorReview vendorReview, bool excludeProperties, bool formatReviewText)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (vendorReview == null)
                throw new ArgumentNullException(nameof(vendorReview));

            var vendor = await _vendorService.GetVendorById(vendorReview.VendorId);
            var customer = await _customerService.GetCustomerById(vendorReview.CustomerId);

            model.Id = vendorReview.Id;
            model.VendorId = vendorReview.VendorId;
            model.VendorName = vendor.Name;
            model.CustomerId = vendorReview.CustomerId;
            model.CustomerInfo = customer != null ? !string.IsNullOrEmpty(customer.Email) ? customer.Email : _translationService.GetResource("Admin.Customers.Guest") : "";
            model.Rating = vendorReview.Rating;
            model.CreatedOn = _dateTimeService.ConvertToUserTime(vendorReview.CreatedOnUtc, DateTimeKind.Utc);
            if (!excludeProperties)
            {
                model.Title = vendorReview.Title;
                if (formatReviewText)
                    model.ReviewText = FormatText.ConvertText(vendorReview.ReviewText);
                else
                    model.ReviewText = vendorReview.ReviewText;
                model.IsApproved = vendorReview.IsApproved;
            }
        }

        public virtual async Task PrepareVendorAddressModel(VendorModel model, Vendor vendor)
        {

            if (model.Address == null)
                model.Address = new Models.Common.AddressModel();

            model.Address.FirstNameEnabled = false;
            model.Address.FirstNameRequired = false;
            model.Address.LastNameEnabled = false;
            model.Address.LastNameRequired = false;
            model.Address.EmailEnabled = false;
            model.Address.EmailRequired = false;
            model.Address.CompanyEnabled = _vendorSettings.AddressSettings.CompanyEnabled;
            model.Address.CountryEnabled = _vendorSettings.AddressSettings.CountryEnabled;
            model.Address.StateProvinceEnabled = _vendorSettings.AddressSettings.StateProvinceEnabled;
            model.Address.CityEnabled = _vendorSettings.AddressSettings.CityEnabled;
            model.Address.CityRequired = _vendorSettings.AddressSettings.CityRequired;
            model.Address.StreetAddressEnabled = _vendorSettings.AddressSettings.StreetAddressEnabled;
            model.Address.StreetAddressRequired = _vendorSettings.AddressSettings.StreetAddressRequired;
            model.Address.StreetAddress2Enabled = _vendorSettings.AddressSettings.StreetAddress2Enabled;
            model.Address.ZipPostalCodeEnabled = _vendorSettings.AddressSettings.ZipPostalCodeEnabled;
            model.Address.ZipPostalCodeRequired = _vendorSettings.AddressSettings.ZipPostalCodeRequired;
            model.Address.PhoneEnabled = _vendorSettings.AddressSettings.PhoneEnabled;
            model.Address.PhoneRequired = _vendorSettings.AddressSettings.PhoneRequired;
            model.Address.FaxEnabled = _vendorSettings.AddressSettings.FaxEnabled;
            model.Address.FaxRequired = _vendorSettings.AddressSettings.FaxRequired;
            model.Address.NoteEnabled = _vendorSettings.AddressSettings.NoteEnabled;
            model.Address.AddressTypeEnabled = false;

            //address
            model.Address.AvailableCountries.Add(new SelectListItem { Text = _translationService.GetResource("Admin.Address.SelectCountry"), Value = "" });
            foreach (var c in await _countryService.GetAllCountries(showHidden: true))
                model.Address.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString(), Selected = (vendor != null && c.Id == vendor.Address.CountryId) });

            var states = !String.IsNullOrEmpty(model.Address.CountryId) ? (await _countryService.GetCountryById(model.Address.CountryId))?.StateProvinces : new List<StateProvince>();
            if (states.Count > 0)
            {
                foreach (var s in states)
                    model.Address.AvailableStates.Add(new SelectListItem { Text = s.Name, Value = s.Id.ToString(), Selected = (vendor != null && s.Id == vendor.Address.StateProvinceId) });
            }
        }

        public virtual async Task PrepareStore(VendorModel model)
        {
            model.AvailableStores.Add(new SelectListItem
            {
                Text = "[None]",
                Value = ""
            });

            foreach (var s in await _storeService.GetAllStores())
            {
                model.AvailableStores.Add(new SelectListItem
                {
                    Text = s.Shortcut,
                    Value = s.Id.ToString()
                });
            }
        }
        public virtual async Task<VendorModel> PrepareVendorModel()
        {
            var model = new VendorModel();
            //discounts
            await PrepareDiscountModel(model, null, true);
            //default values
            model.PageSize = 6;
            model.Active = true;
            model.AllowCustomersToSelectPageSize = true;
            model.PageSizeOptions = _vendorSettings.DefaultVendorPageSizeOptions;

            //default value
            model.Active = true;

            //stores
            await PrepareStore(model);

            //prepare address model
            await PrepareVendorAddressModel(model, null);
            return model;
        }
        public virtual async Task<IList<VendorModel.AssociatedCustomerInfo>> AssociatedCustomers(string vendorId)
        {
            return (await _customerService
                .GetAllCustomers(vendorId: vendorId))
                .Select(c => new VendorModel.AssociatedCustomerInfo()
                {
                    Id = c.Id,
                    Email = c.Email
                })
                .ToList();
        }
        public virtual async Task<Vendor> InsertVendorModel(VendorModel model)
        {
            var vendor = model.ToEntity();
            vendor.Address = model.Address.ToEntity();
            vendor.Address.CreatedOnUtc = DateTime.UtcNow;

            await _vendorService.InsertVendor(vendor);

            //discounts
            var allDiscounts = await _discountService.GetAllDiscounts(DiscountType.AssignedToVendors, showHidden: true);
            foreach (var discount in allDiscounts)
            {
                if (model.SelectedDiscountIds != null && model.SelectedDiscountIds.Contains(discount.Id))
                    vendor.AppliedDiscounts.Add(discount.Id);
            }

            //search engine name
            model.SeName = await vendor.ValidateSeName(model.SeName, vendor.Name, true, _seoSettings, _slugService, _languageService);
            vendor.Locales = await model.Locales.ToTranslationProperty(vendor, x => x.Name, _seoSettings, _slugService, _languageService);
            vendor.SeName = model.SeName;
            await _vendorService.UpdateVendor(vendor);

            //update picture seo file name
            await _pictureService.UpdatePictureSeoNames(vendor.PictureId, vendor.Name);
            await _slugService.SaveSlug(vendor, model.SeName, "");

            return vendor;
        }
        public virtual async Task<Vendor> UpdateVendorModel(Vendor vendor, VendorModel model)
        {
            string prevPictureId = vendor.PictureId;
            vendor = model.ToEntity(vendor);
            vendor.Locales = await model.Locales.ToTranslationProperty(vendor, x => x.Name, _seoSettings, _slugService, _languageService);
            model.SeName = await vendor.ValidateSeName(model.SeName, vendor.Name, true, _seoSettings, _slugService, _languageService);
            vendor.Address = model.Address.ToEntity(vendor.Address);

            //discounts
            var allDiscounts = await _discountService.GetAllDiscounts(DiscountType.AssignedToVendors, showHidden: true);
            foreach (var discount in allDiscounts)
            {
                if (model.SelectedDiscountIds != null && model.SelectedDiscountIds.Contains(discount.Id))
                {
                    //new discount
                    if (vendor.AppliedDiscounts.Count(d => d == discount.Id) == 0)
                        vendor.AppliedDiscounts.Add(discount.Id);
                }
                else
                {
                    //remove discount
                    if (vendor.AppliedDiscounts.Count(d => d == discount.Id) > 0)
                        vendor.AppliedDiscounts.Remove(discount.Id);
                }
            }

            vendor.SeName = model.SeName;

            await _vendorService.UpdateVendor(vendor);
            //search engine name                
            await _slugService.SaveSlug(vendor, model.SeName, "");

            //delete an old picture (if deleted or updated)
            if (!String.IsNullOrEmpty(prevPictureId) && prevPictureId != vendor.PictureId)
            {
                var prevPicture = await _pictureService.GetPictureById(prevPictureId);
                if (prevPicture != null)
                    await _pictureService.DeletePicture(prevPicture);
            }
            //update picture seo file name
            await _pictureService.UpdatePictureSeoNames(vendor.PictureId, vendor.Name);
            return vendor;
        }
        public virtual async Task DeleteVendor(Vendor vendor)
        {
            //clear associated customer references
            var associatedCustomers = await _customerService.GetAllCustomers(vendorId: vendor.Id);
            foreach (var customer in associatedCustomers)
            {
                customer.VendorId = "";
                await _customerService.UpdateCustomer(customer);
            }
            await _vendorService.DeleteVendor(vendor);
        }
        public virtual IList<VendorModel.VendorNote> PrepareVendorNote(Vendor vendor)
        {
            var vendorNoteModels = new List<VendorModel.VendorNote>();
            foreach (var vendorNote in vendor.VendorNotes
                .OrderByDescending(vn => vn.CreatedOnUtc))
            {
                vendorNoteModels.Add(new VendorModel.VendorNote
                {
                    Id = vendorNote.Id,
                    VendorId = vendor.Id,
                    Note = vendorNote.Note,
                    CreatedOn = _dateTimeService.ConvertToUserTime(vendorNote.CreatedOnUtc, DateTimeKind.Utc)
                });
            }
            return vendorNoteModels;
        }
        public virtual async Task<bool> InsertVendorNote(string vendorId, string message)
        {
            var vendor = await _vendorService.GetVendorById(vendorId);
            if (vendor == null)
                return false;

            var vendorNote = new VendorNote
            {
                Note = message,
                CreatedOnUtc = DateTime.UtcNow,
            };
            vendor.VendorNotes.Add(vendorNote);
            await _vendorService.UpdateVendor(vendor);

            return true;
        }
        public virtual async Task DeleteVendorNote(string id, string vendorId)
        {
            var vendor = await _vendorService.GetVendorById(vendorId);
            if (vendor == null)
                throw new ArgumentException("No vendor found with the specified id");

            var vendorNote = vendor.VendorNotes.FirstOrDefault(vn => vn.Id == id);
            if (vendorNote == null)
                throw new ArgumentException("No vendor note found with the specified id");
            await _vendorService.DeleteVendorNote(vendorNote, vendorId);
        }

        public virtual async Task<(IEnumerable<VendorReviewModel> vendorReviewModels, int totalCount)> PrepareVendorReviewModel(VendorReviewListModel model, int pageIndex, int pageSize)
        {
            DateTime? createdOnFromValue = (model.CreatedOnFrom == null) ? null
                            : (DateTime?)_dateTimeService.ConvertToUtcTime(model.CreatedOnFrom.Value, _dateTimeService.CurrentTimeZone);

            DateTime? createdToFromValue = (model.CreatedOnTo == null) ? null
                            : (DateTime?)_dateTimeService.ConvertToUtcTime(model.CreatedOnTo.Value, _dateTimeService.CurrentTimeZone).AddDays(1);

            IPagedList<VendorReview> vendorReviews = await _vendorService.GetAllVendorReviews("", null,
                     createdOnFromValue, createdToFromValue, model.SearchText, model.SearchVendorId, pageIndex - 1, pageSize);
            var items = new List<VendorReviewModel>();
            foreach (var x in vendorReviews)
            {
                var m = new VendorReviewModel();
                await PrepareVendorReviewModel(m, x, false, true);
                items.Add(m);
            }
            return (items, vendorReviews.TotalCount);
        }
        public virtual async Task<VendorReview> UpdateVendorReviewModel(VendorReview vendorReview, VendorReviewModel model)
        {
            vendorReview.Title = model.Title;
            vendorReview.ReviewText = model.ReviewText;
            vendorReview.IsApproved = model.IsApproved;

            await _vendorService.UpdateVendorReview(vendorReview);

            var vendor = await _vendorService.GetVendorById(vendorReview.VendorId);
            //update vendor totals
            await _vendorService.UpdateVendorReviewTotals(vendor);
            return vendorReview;
        }
        public virtual async Task DeleteVendorReview(VendorReview vendorReview)
        {
            await _vendorService.DeleteVendorReview(vendorReview);
            var vendor = await _vendorService.GetVendorById(vendorReview.VendorId);
            //update vendor totals
            await _vendorService.UpdateVendorReviewTotals(vendor);
        }
        public virtual async Task ApproveVendorReviews(IList<string> selectedIds)
        {
            foreach (var id in selectedIds)
            {
                string idReview = id.Split(':').First().ToString();
                string idVendor = id.Split(':').Last().ToString();
                var vendor = await _vendorService.GetVendorById(idVendor);
                var vendorReview = await _vendorService.GetVendorReviewById(idReview);
                if (vendorReview != null)
                {
                    var previousIsApproved = vendorReview.IsApproved;
                    vendorReview.IsApproved = true;
                    await _vendorService.UpdateVendorReview(vendorReview);
                    await _vendorService.UpdateVendorReviewTotals(vendor);

                    //raise event (only if it wasn't approved before)
                    if (!previousIsApproved)
                        await _mediator.Publish(new VendorReviewApprovedEvent(vendorReview));
                }
            }
        }
        public virtual async Task DisapproveVendorReviews(IList<string> selectedIds)
        {
            foreach (var id in selectedIds)
            {
                string idReview = id.Split(':').First().ToString();
                string idVendor = id.Split(':').Last().ToString();

                var vendor = await _vendorService.GetVendorById(idVendor);
                var vendorReview = await _vendorService.GetVendorReviewById(idReview);
                if (vendorReview != null)
                {
                    vendorReview.IsApproved = false;
                    await _vendorService.UpdateVendorReview(vendorReview);
                    await _vendorService.UpdateVendorReviewTotals(vendor);
                }
            }
        }
    }
}
