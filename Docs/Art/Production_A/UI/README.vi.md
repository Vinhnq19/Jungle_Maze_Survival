# Production A · P01 / P02 / P06 / P07

Ngày **27/10/2026**. Trạng thái: **asset PNG đã chuẩn bị và kiểm file; Unity binding/import và nghiệm thu do bước integration/QA xác nhận**.

Người dùng đã yêu cầu sản xuất và gắn asset sau khi chọn **A charcoal/jade/soft gold**. Nguồn bố cục/màu:[A gốc](../../directions/A_figma_original.svg),[A recolor](../../directions/A_charcoal_jade.svg). Đợt này chỉ tạo/thay PNG và nguồn tái tạo;không sửa C#,prefab,.meta,manifest hoặc world gameplay.

## Bàn giao

- [Mapping máy đọc](asset_mapping.json):đường dẫn,size,pack,role,tint,borderLTRB,PPU100,Point/no mipmap/no compression đề xuất. Integration giữ GUID.meta cũ và tạo.meta mới khi cần.
- [Kiểm file PNG](png_validation.json):32PNG xuất RGBA,đúng kích thước;mọi pixel có alpha>0 của mask đều RGB255/255/255. Không bake màu vào mask rồi nhân tint lần nữa.
- [Effect contact18/28/40px](Effects_contact.png),[mask contact](Masks_contact.png),[menu contact](Menu_contact.png):đã mở kiểm trực quan,7effect có silhouette khác nhau và đọc được ở các cỡ hiển thị.
- Script tái tạo:`Tools/Art/build_a_ui_assets.py` dùng Pillow;không gọi Unity và không chỉnh.meta.

**Số lượng:**30dòng UI trong manifest thuộc P01(11),P02(6),P06(7),P07(6);trong đó2ảnh P02 tái sử dụng nguyên trạng. Tạo/thay28PNG chính,thêm4overlay phục vụ tint đúng A=tổng32PNG sản xuất;2PNG reuse ngoài số này.

## P01 — base và overlay độc lập

Base`HudRound10.png`96²,`HudRound6.png`128² và các fill là **mask trắng phẳng**. Component Image tint Surface/SelectedFill hoặc màu ngữ nghĩa. Outline selected`HudRound6Stroke2.png`128² vẫn trong suốt ở tâm, chỉ viền trắng để tint gold`#C6AE78`;cũng an toàn cho InUseFrame overlay.

| Overlay mới | Border LTRB | Image tint | Vai trò |
| --- | --- | --- | --- |
| `ABevel96.png` |24,24,24,24|`#526B69`|Mép sáng trên/trái panel |
| `AFrameEdge96.png` |24,24,24,24|`#0C1417`|Viền tối panel |
| `ABevelSlot128.png` |20,20,20,20|`#526B69`|Mép sáng trên/trái slot |
| `AFrameEdgeSlot128.png` |20,20,20,20|`#0C1417`|Viền tối slot |

Các overlay trắng có tâm trong suốt;gắn Image con cùng rect,không raycast,Image.Type Sliced. Thứ tự base→edge→bevel→selected outline. Tách lớp vì tint Surface tối`#202A2D`không thể tự nhân ra bevel sáng`#526B69`. Integration chịu trách nhiệm gắn đúng lớp/tint,không coi PNG đơn đã cho ra toàn bộ màu A.

`HudRound6Dashed.png`128² giữ nét đứt/tâm trong;`HudRound6Stroke1.png`96²border16;marker`HudCircle16.png`đổi16→64²,theo manifest;outline cũng đổi16→96². Cần import lại và cập nhật border đúng mapping,kích thước rect prefab giữ nguyên. BarFill/BarTrack64×16,KeyCap48²,MinimapFrame400²,Crosshair128² là mask trắng riêng.

## P02 — giữ hình học cảnh báo

Tạo HitMarker128²,EffectFrame88²border18,ToastBg96×64border20,DangerBanner192×80border28. Màu do Image tint/semantic palette quyết định.

**Reuse nguyên PNG/meta**`HudArcFilled.png`88×232 và`HudArrowOutline.png`120². Không tạo DamageArc/DangerArrow alias trùng,không thay geometry/pivot. Parent cập nhật mapping manifest sang2path hiện có nếu cần. Các file này không thuộc diff asset của đợt này.

## P06 — hiệu ứng

7icon native32² được author bằng pixel geometry,tăng nearest8× thành256²RGBA:Poison(drop),Slow(boot),Invisible(fading silhouette),Stun(stars),Root(vine-bound legs),Reverse(opposed arrows),CcImmune(shield/check). Nguồn native32 nằm ngay thư mục báo cáo. Đây là colored art:Image tint trắng,không tint cả icon theo màu debuff/buff;semantic tint áp vào khung/chip. Adrenaline reuse icon equipment từ worker vật phẩm,không tạo bản thứ8trùng.

## P07 — menu

- Logo1024×256RGBA:đúng chữ LASTAWAY,Pixelify Sans Bold,native256×64,alpha nhị phân rồi nearest4×;màu soft gold.
- ButtonPrimary/Secondary/Ghost192×96,border32:empty face,không bake chữ. Primary jade`#3B8D7B`viền gold;secondary charcoal;ghost alpha68%. Art đã có màu nên Image tint **trắng**.
- Background1920×1080RGBA:native320×180nearest6×,rừng tối ít tương phản,không có chữ/UI.
- KeyArt1040²RGBA:native208²nearest5×,đảo rừng nguyên bản nhìn từ trên,survivor giữa và vòng bo tím. Bổ sung phần minh họa còn là placeholder trong A;không sửa bố cục Figma/logic/world của game. Không dùng lại art H/J/K/L/M.

Menu chỉ là asset pack chuẩn bị dùng trong UI-02;không tự triển khai luồng/menu logic. PNG/menu chưa đồng nghĩa đã gắn vào scene chính.

## Kiểm tra và retry

32PNG đã qua script kiểm modeRGBA,size và trắng trung tính của mask;7effect contact ở18/28/40px,mask/menu contact đã xem. Mọi output ở path game;nguồn và mapping ở repo. **Chưa đo PlayMode/Unity import/binding** trong vai trò asset-only.

Có1lỗi save Windows`Invalid argument`đối với HudRound6Stroke2 khi Unity có thể đang import song song;chạy lại1lần thành công,không lặp thêm. Không thay.meta để vượt lỗi. Tối đa3vòng/issue theo yêu cầu người dùng;đợt này đã xong trong giới hạn.
