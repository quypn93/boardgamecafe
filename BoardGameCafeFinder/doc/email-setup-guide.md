# Email Setup Guide - support@boardgamecafefinder.com

## Overview

This guide explains how to set up a professional email address `support@boardgamecafefinder.com` for the BoardGame Cafe Finder application.

---

## Option 1: Zoho Mail (Recommended - Free)

### Step 1: Purchase Domain

If you don't already own `boardgamecafefinder.com`, purchase it from:
- [Namecheap](https://namecheap.com) (~$10-15/year)
- [Cloudflare Registrar](https://cloudflare.com)
- [GoDaddy](https://godaddy.com)

### Step 2: Sign Up for Zoho Mail

1. Go to [Zoho Mail Pricing](https://www.zoho.com/mail/zohomail-pricing.html)
2. Select **Forever Free Plan** (5 users, 5GB per user)
3. Enter domain: `boardgamecafefinder.com`
4. Create first email: `support@boardgamecafefinder.com`

### Step 3: Configure DNS Records

Add these records in your domain's DNS manager:

#### TXT Record (Domain Verification)
```
Type: TXT
Host: @
Value: zoho-verification=zb12345678.zmverify.zoho.com
TTL: 3600
```
*Note: Zoho will provide the exact verification value*

#### MX Records (Receive Email)
| Type | Host | Value | Priority | TTL |
|------|------|-------|----------|-----|
| MX | @ | mx.zoho.com | 10 | 3600 |
| MX | @ | mx2.zoho.com | 20 | 3600 |
| MX | @ | mx3.zoho.com | 50 | 3600 |

#### SPF Record (Send Email - Prevent Spam)
```
Type: TXT
Host: @
Value: v=spf1 include:zoho.com ~all
TTL: 3600
```

#### DKIM Record (Optional but Recommended)
Zoho will provide a DKIM record to add. It looks like:
```
Type: TXT
Host: zmail._domainkey
Value: v=DKIM1; k=rsa; p=MIGfMA0GCS...
TTL: 3600
```

### Step 4: Create App Password

1. Login to [Zoho Mail](https://mail.zoho.com)
2. Go to **Settings** (gear icon)
3. **Mail Accounts** > Select your account
4. **Security** > **App Passwords**
5. Generate a new app password for "BoardGame Cafe Finder App"
6. Copy the 16-character password

### Step 5: Update Application Configuration

Edit `appsettings.json`:

```json
"Email": {
  "RequireConfirmation": true,
  "SmtpHost": "smtp.zoho.com",
  "SmtpPort": 587,
  "SmtpUser": "support@boardgamecafefinder.com",
  "SmtpPassword": "YOUR_ZOHO_APP_PASSWORD",
  "FromEmail": "support@boardgamecafefinder.com",
  "FromName": "BoardGame Cafe Finder"
}
```

### Zoho SMTP Settings Reference
| Setting | Value |
|---------|-------|
| SMTP Host | smtp.zoho.com |
| SMTP Port | 587 (TLS) or 465 (SSL) |
| Username | support@boardgamecafefinder.com |
| Password | App Password (16 chars) |
| Encryption | TLS/STARTTLS |

---

## Option 2: Google Workspace ($6/month)

### Setup Steps
1. Go to [Google Workspace](https://workspace.google.com)
2. Sign up with your domain
3. Verify domain ownership via DNS
4. Create user: support@boardgamecafefinder.com

### DNS Records for Google Workspace

#### MX Records
| Priority | Value |
|----------|-------|
| 1 | ASPMX.L.GOOGLE.COM |
| 5 | ALT1.ASPMX.L.GOOGLE.COM |
| 5 | ALT2.ASPMX.L.GOOGLE.COM |
| 10 | ALT3.ASPMX.L.GOOGLE.COM |
| 10 | ALT4.ASPMX.L.GOOGLE.COM |

### SMTP Settings for Google Workspace
```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUser": "support@boardgamecafefinder.com",
  "SmtpPassword": "YOUR_GOOGLE_APP_PASSWORD",
  "FromEmail": "support@boardgamecafefinder.com",
  "FromName": "BoardGame Cafe Finder"
}
```

---

## Option 3: Cloudflare Email Routing (Free - Forward Only)

This option forwards emails to an existing Gmail but allows sending FROM your custom domain.

### Setup
1. Add domain to Cloudflare (free plan)
2. Go to Email > Email Routing
3. Create route: support@boardgamecafefinder.com -> your-gmail@gmail.com
4. Use Gmail SMTP with "Send mail as" feature

### Limitations
- Requires Gmail account for sending
- More complex setup
- May have deliverability issues

---

## Testing Email Configuration

### Test 1: Send Test Email
```csharp
// In any controller or service
await _emailService.SendEmailAsync(
    "your-test-email@gmail.com",
    "Test Email",
    "<h1>Test</h1><p>Email is working!</p>"
);
```

### Test 2: Register New User
1. Go to /Account/Register
2. Create a new account
3. Check inbox for confirmation email
4. Click confirmation link

### Common Issues

| Issue | Solution |
|-------|----------|
| Email not sending | Check SMTP credentials and port |
| Email going to spam | Add SPF and DKIM records |
| Connection timeout | Try port 465 with SSL instead of 587 |
| Authentication failed | Regenerate app password |

---

## Email Features in Application

The application uses email for:

1. **Email Confirmation** (Registration)
   - Sent when user registers
   - Contains confirmation link
   - Template in `EmailService.SendEmailConfirmationAsync()`

2. **Password Reset** (Future)
   - Not yet implemented

3. **Cafe Claim Notifications** (Future)
   - Notify owners when their cafe is claimed

---

## Cost Summary

| Option | Monthly Cost | Annual Cost |
|--------|--------------|-------------|
| Zoho Mail Free | $0 | $0 |
| Google Workspace | $6 | $72 |
| Microsoft 365 | $6 | $72 |
| Domain Registration | - | $10-15 |

**Recommended**: Zoho Mail Free + Domain = ~$12/year total

---

## Current Configuration

File: `appsettings.json`

```json
"Email": {
  "RequireConfirmation": true,
  "SmtpHost": "smtp.zoho.com",
  "SmtpPort": 587,
  "SmtpUser": "support@boardgamecafefinder.com",
  "SmtpPassword": "YOUR_ZOHO_APP_PASSWORD",
  "FromEmail": "support@boardgamecafefinder.com",
  "FromName": "BoardGame Cafe Finder"
}
```

**Action Required**: Replace `YOUR_ZOHO_APP_PASSWORD` with actual app password after completing Zoho setup.

---

## Security Notes

1. **Never commit real passwords** to version control
2. Use **environment variables** or **User Secrets** in production:
   ```bash
   dotnet user-secrets set "Email:SmtpPassword" "your-real-password"
   ```
3. Consider using **Azure Key Vault** or similar for production deployments
