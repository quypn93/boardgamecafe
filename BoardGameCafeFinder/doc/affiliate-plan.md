# Affiliate Plan - BoardGame Cafe Finder

## Tổng Quan

Triển khai hệ thống affiliate để kiếm hoa hồng từ việc giới thiệu người dùng mua board games.

---

## Phase 1: Hiển Thị Affiliate Links (Đã hoàn thành)

### 1.1 Board Game Library trong Cafe Details
- **File:** `Views/Cafe/Details.cshtml`
- Hiển thị nút "Buy" cho games có `AmazonAffiliateUrl`
- Sử dụng `rel="nofollow sponsored"` theo chuẩn SEO Google
- Link mở trong tab mới

### 1.2 Admin CafeGames View
- **File:** `Views/Admin/CafeGames.cshtml`
- Thêm cột "Affiliate" để admin thấy games nào có link
- Badge màu vàng cho games có affiliate URL

---

## Phase 2: Quản Lý Affiliate URLs

### 2.1 Admin Edit Game
Cho phép admin chỉnh sửa affiliate URL của từng game:
- Tạo view `Views/Admin/EditGame.cshtml`
- Thêm action trong `AdminController`
- Form validate URL format

### 2.2 Bulk Import
Import affiliate URLs từ CSV:
```
BGGId,AmazonAffiliateUrl
123456,https://amazon.com/dp/xxx?tag=yourcode
789012,https://amazon.com/dp/yyy?tag=yourcode
```

### 2.3 Auto-Generate Affiliate URLs
Tự động tạo affiliate URL từ tên game:
- Sử dụng Amazon Product Advertising API
- Hoặc pattern URL với affiliate tag

---

## Phase 3: Tracking & Analytics

### 3.1 Click Tracking
Redirect qua internal endpoint để đếm clicks:
```
/affiliate/redirect?gameId=123&url=encoded_url
```

Database table:
```sql
CREATE TABLE AffiliateClicks (
    Id INT PRIMARY KEY IDENTITY,
    GameId INT,
    CafeId INT,
    UserId INT NULL,
    ClickedAt DATETIME,
    IpAddress VARCHAR(50),
    UserAgent VARCHAR(500)
)
```

### 3.2 Dashboard
- Số clicks theo game/cafe/ngày
- Top games được click nhiều nhất
- Conversion tracking (nếu có)

---

## Phase 4: Mở Rộng Affiliate

### 4.1 Nhiều Affiliate Partners
- **Amazon** (đã có) - 1-4% commission, 24h cookie
- **Miniature Market** - 5% commission, chuyên board games
- **CoolStuffInc** - 5% commission
- **Philibert** - 5% commission, 7 ngày cookie (EU)
- **Casual Game Revolution** - 20% commission, 30 ngày cookie
- Local game stores - partnership trực tiếp

> **Lưu ý:** BoardGameGeek không có chương trình affiliate (chỉ là database/community)

### 4.2 Blog Integration
- Thêm affiliate links trong blog posts
- Widget "Games mentioned in this post"
- Related games suggestions

### 4.3 Wishlist Feature
- User tạo wishlist games yêu thích
- Nút "Buy" cho mỗi game trong wishlist
- Email reminder với affiliate links

---

## Cấu Hình Amazon Affiliate

### Đăng ký
1. Đăng ký Amazon Associates: https://affiliate-program.amazon.com/
2. Lấy Associate Tag (vd: `bgcfinder-20`)
3. Cấu hình trong appsettings.json:

```json
{
  "Affiliate": {
    "Amazon": {
      "AssociateTag": "bgcfinder-20",
      "BaseUrl": "https://www.amazon.com/dp/"
    }
  }
}
```

### URL Format
```
https://www.amazon.com/dp/{ASIN}?tag={AssociateTag}
```

---

## Model Hiện Tại

```csharp
// BoardGame.cs - Line 44
[MaxLength(1000)]
[Url]
public string? AmazonAffiliateUrl { get; set; }
```

---

## Files Đã Sửa

| File | Thay đổi |
|------|----------|
| `Views/Cafe/Details.cshtml` | Thêm nút Buy affiliate |
| `Views/Admin/CafeGames.cshtml` | Thêm cột Affiliate URL |

---

## Lưu Ý Pháp Lý

1. **FTC Disclosure**: Cần thông báo rằng website nhận hoa hồng từ affiliate links
2. **GDPR**: Tracking clicks cần tuân thủ privacy policy
3. **Amazon TOS**: Không được che giấu affiliate links là quảng cáo

### Ví dụ Disclosure
```html
<small class="text-muted">
    * As an Amazon Associate, we earn from qualifying purchases.
</small>
```

---

## Ước Tính Doanh Thu

| Metrics | Giá trị |
|---------|---------|
| Avg order value | $40 |
| Commission rate | 4% |
| Conversion rate | 2-3% |
| Monthly clicks | 1000 |
| Est. monthly revenue | $40 x 0.04 x 0.025 x 1000 = $40 |

---

## Timeline

- [x] Phase 1: Hiển thị affiliate links (Hoàn thành)
- [ ] Phase 2: Admin management
- [ ] Phase 3: Tracking & Analytics
- [ ] Phase 4: Mở rộng partners
