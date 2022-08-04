using System;
using System.Collections.Generic;
using Vendr.Core.Api;
using Vendr.Core.PaymentProviders;
using Vendr.PaymentProviders.Opayo.Api;
using Vendr.PaymentProviders.Opayo.Api.Models;
using Vendr.Common.Logging;
using System.Threading.Tasks;
using Vendr.Extensions;

namespace Vendr.PaymentProviders.Opayo
{
    // NOTE: This payment provider was written just as SagePay was rebranded to Opayo so we
    // have decided to upate the payment provider name to Opayo to make it future proof however
    // much of the API endpoints are still referencing SagePay 
    [PaymentProvider("opayo-server", "Opayo Server", "Opayo Server payment provider", Icon = "icon-credit-card")]
    public class OpayoServerPaymentProvider : PaymentProviderBase<OpayoSettings>
    {
        private readonly ILogger<OpayoServerPaymentProvider> _logger;

        public OpayoServerPaymentProvider(VendrContext vendr, ILogger<OpayoServerPaymentProvider> logger)
            : base(vendr)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override string GetCancelUrl(PaymentProviderContext<OpayoSettings> ctx)
        {
            ctx.Settings.MustNotBeNull(nameof(ctx.Settings));
            ctx.Settings.CancelUrl.MustNotBeNullOrWhiteSpace(nameof(ctx.Settings.CancelUrl));

            return ctx.Settings.CancelUrl.ReplacePlaceHolders(ctx.Order);
        }

        public override string GetErrorUrl(PaymentProviderContext<OpayoSettings> ctx)
        {
            ctx.Settings.MustNotBeNull(nameof(ctx.Settings));
            ctx.Settings.ErrorUrl.MustNotBeNullOrWhiteSpace(nameof(ctx.Settings.ErrorUrl));

            return ctx.Settings.ErrorUrl.ReplacePlaceHolders(ctx.Order);
        }

        public override string GetContinueUrl(PaymentProviderContext<OpayoSettings> ctx)
        {
            ctx.Settings.MustNotBeNull(nameof(ctx.Settings));
            ctx.Settings.ContinueUrl.MustNotBeNullOrWhiteSpace(nameof(ctx.Settings.ContinueUrl));

            return ctx.Settings.ContinueUrl.ReplacePlaceHolders(ctx.Order);
        }

        public override async Task<PaymentFormResult> GenerateFormAsync(PaymentProviderContext<OpayoSettings> ctx)
        {
            var form = new PaymentForm(ctx.Urls.CancelUrl, PaymentFormMethod.Post);
            var client = new OpayoServerClient(_logger, new OpayoServerClientConfig
            {
                ContinueUrl = ctx.Urls.ContinueUrl,
                CancelUrl = ctx.Urls.CancelUrl,
                ErrorUrl = ctx.Urls.ErrorUrl,
                ProviderAlias = Alias
            });

            var inputFields = OpayoInputLoader.LoadInputs(ctx.Order, ctx.Settings, Vendr, ctx.Urls.CallbackUrl);
            var responseDetails = await client.InitiateTransactionAsync(ctx.Settings.TestMode, inputFields)
                .ConfigureAwait(false);

            var status = responseDetails[OpayoConstants.Response.Status];

            Dictionary<string, string> orderMetaData = null;

            if (status == OpayoConstants.Response.StatusCodes.Ok || status == OpayoConstants.Response.StatusCodes.Repeated)
            {
                orderMetaData = new Dictionary<string, string>
                {
                    { OpayoConstants.OrderProperties.SecurityKey, responseDetails[OpayoConstants.Response.SecurityKey] },
                    { OpayoConstants.OrderProperties.TransactionId, responseDetails[OpayoConstants.Response.TransactionId]}
                };

                form.Action = responseDetails[OpayoConstants.Response.NextUrl];
            }
            else
            {
                _logger.Warn("Opayo (" + ctx.Order.OrderNumber + ") - Generate html form error - status: " + status + " | status details: " + responseDetails["StatusDetail"]);
            }

            return new PaymentFormResult()
            {
                MetaData = orderMetaData,
                Form = form
            };
        }

        public override async Task<CallbackResult> ProcessCallbackAsync(PaymentProviderContext<OpayoSettings> ctx)
        {
            var callbackRequestModel = await CallbackRequestModel.FromRequestAsync(ctx.Request);
            var client = new OpayoServerClient(
                _logger, 
                new OpayoServerClientConfig {
                    ProviderAlias = Alias,
                    ContinueUrl = ctx.Urls.ContinueUrl,
                    CancelUrl = ctx.Urls.CancelUrl,
                    ErrorUrl = ctx.Urls.ErrorUrl
                });

            return client.HandleCallback(ctx.Order, callbackRequestModel, ctx.Settings);

        }
    }


    // Maintain a wrapper sage pay provider that just proxies the Opayo provider so that
    // we don't break anyone using the alpha. Thankfully non of the ctx.Settings were prefixed
    // with "SagePay" so it shouldn't be a problem reusing Opayo ctx.Settings object
    [Obsolete("Use OpayoServerPaymentProvider instead")]
    [PaymentProvider("sagepay-server", "Opayo Server", "Opayo Server payment provider", Icon = "icon-credit-card")]
    public class SagePayServerPaymentProvider : OpayoServerPaymentProvider
    {
        public SagePayServerPaymentProvider(VendrContext vendr, ILogger<OpayoServerPaymentProvider> logger)
            : base(vendr, logger)
        { }
    }
}
