# LemonSqueezy Integration - Setup Guide

## Overview

LemonSqueezy has been integrated as the payment provider for **non-Vietnam users**.
- Users with Vietnamese language (culture `vi`) will continue using the existing payment method (PayPal/Stripe).
- All other users will be redirected to LemonSqueezy checkout.

---

## Steps to Complete

### 1. Create a LemonSqueezy Account & Store

- Sign up at https://lemonsqueezy.com
- Create a store for "BoardGame Cafe Finder"
- Note your **Store ID** from the store settings

### 2. Create Products & Variants

You need to create **3 products** (one per plan) with **4 variants each** (one per duration):

| Product       | Variant     | Price (USD)  | Description                          |
|---------------|-------------|--------------|--------------------------------------|
| **Basic**     | 1 month     | $29.99       | Basic Plan - 1 month                 |
| **Basic**     | 3 months    | $85.47       | Basic Plan - 3 months (5% off)       |
| **Basic**     | 6 months    | $161.95      | Basic Plan - 6 months (10% off)      |
| **Basic**     | 12 months   | $287.90      | Basic Plan - 12 months (20% off)     |
| **Premium**   | 1 month     | $59.99       | Premium Plan - 1 month               |
| **Premium**   | 3 months    | $170.97      | Premium Plan - 3 months (5% off)     |
| **Premium**   | 6 months    | $323.95      | Premium Plan - 6 months (10% off)    |
| **Premium**   | 12 months   | $575.90      | Premium Plan - 12 months (20% off)   |
| **Featured**  | 1 month     | $99.99       | Featured Plan - 1 month              |
| **Featured**  | 3 months    | $284.97      | Featured Plan - 3 months (5% off)    |
| **Featured**  | 6 months    | $539.95      | Featured Plan - 6 months (10% off)   |
| **Featured**  | 12 months   | $959.90      | Featured Plan - 12 months (20% off)  |

For each variant, set it as a **one-time payment** (not subscription).

### 3. Get Checkout URLs

After creating each variant, get its checkout URL from the LemonSqueezy dashboard.
The URL format is typically: `https://[your-store].lemonsqueezy.com/checkout/buy/[variant-id]`

### 4. Get API Key

- Go to **Settings > API** in LemonSqueezy dashboard
- Create a new API key
- Copy the key

### 5. Configure Webhook

- Go to **Settings > Webhooks** in LemonSqueezy dashboard
- Create a new webhook with:
  - **URL**: `https://your-domain.com/Listing/LemonSqueezyWebhook`
  - **Events**: Select `order_created`
  - **Secret**: Create a signing secret and copy it

### 6. Update `appsettings.json` (or `appsettings.Local.json`)

Fill in the placeholder values:

```json
"LemonSqueezy": {
  "ApiKey": "your-actual-api-key",
  "StoreId": "your-store-id",
  "WebhookSecret": "your-webhook-signing-secret",
  "CheckoutUrls": {
    "Basic_1": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-1",
    "Basic_3": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-2",
    "Basic_6": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-3",
    "Basic_12": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-4",
    "Premium_1": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-5",
    "Premium_3": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-6",
    "Premium_6": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-7",
    "Premium_12": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-8",
    "Featured_1": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-9",
    "Featured_3": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-10",
    "Featured_6": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-11",
    "Featured_12": "https://yourstore.lemonsqueezy.com/checkout/buy/variant-id-12"
  }
}
```

---

## How It Works

### Payment Flow

1. User visits `/Listing/Claim/{cafeId}` and fills in their claim form
2. System detects user's language/culture:
   - `vi` (Vietnamese) -> Uses PayPal/Stripe (existing flow)
   - Any other language -> Uses LemonSqueezy
3. For LemonSqueezy:
   - A checkout URL is built using the configured URL for the selected plan+duration
   - User email, name, and claim request ID are passed as URL parameters
   - User is redirected to LemonSqueezy hosted checkout page
   - After payment, user is redirected back to success page (payment pending confirmation)
   - LemonSqueezy sends a webhook (`order_created`) to `/Listing/LemonSqueezyWebhook`
   - Webhook processes the payment and activates the listing

### Key Files Modified

| File | Changes |
|------|---------|
| `appsettings.json` | Added `LemonSqueezy` config section |
| `Services/LemonSqueezyPaymentService.cs` | New payment service implementation |
| `Services/PaymentServiceFactory.cs` | Added LemonSqueezy + country-based provider selection |
| `Program.cs` | Registered LemonSqueezy in DI, auto-selects provider by user country |
| `Controllers/ListingController.cs` | Country detection, LemonSqueezy webhook handler |
| `Views/Listing/Claim.cshtml` | Shows payment method based on country |
| `Views/Listing/CheckoutSuccess.cshtml` | Handles LemonSqueezy pending payment state |

---

## Checklist

- [ ] Create LemonSqueezy account and store
- [ ] Create 3 products with 4 variants each (12 total)
- [ ] Get checkout URLs for all 12 variants
- [ ] Generate API key
- [ ] Set up webhook pointing to `/Listing/LemonSqueezyWebhook`
- [ ] Fill in all values in `appsettings.json` or `appsettings.Local.json`
- [ ] Test with a non-Vietnamese language setting
- [ ] Verify webhook receives `order_created` events
- [ ] Test end-to-end: claim -> checkout -> webhook -> listing activated
