-- AspNetUsers.PasswordChangedAt: şifre en son ne zaman değişti/sıfırlandı.
-- CustomProfileService.IsActiveAsync, bu andan önce açılmış oturumların (auth_time)
-- yenilenmesini ve IdS'teki access token doğrulamasını reddeder.
-- SIRA: bu script, bu alanı kullanan IdS kodu yayınlanmadan ÖNCE çalıştırılmalı.
-- EF kullanıcı sorgularında bütün kolonları seçer; kolon yokken yeni kod yayınlanırsa
-- girişler dahil kullanıcı okuyan her istek hata verir. Eski kod bu kolonu yok sayar.
-- Çalıştırma sırası: 1) kontrol  2) ekleme  3) doğrulama.  Geri alma en altta.

-- 1) KONTROL (salt okunur) ---------------------------------------------------
SELECT column_name FROM information_schema.columns
WHERE table_schema = 'ids4' AND table_name = 'AspNetUsers' AND column_name = 'PasswordChangedAt';   -- 0 satır beklenir

-- 2) EKLEME (tek transaction, tekrar çalıştırılabilir) -------------------------
-- Boş (NULL) kolon eklemek tabloyu yeniden yazmaz; kısa bir kilit alır.
-- Mevcut kullanıcılarda NULL kalır = kısıt yok; ilk şifre değişikliğinde dolar.
BEGIN;
ALTER TABLE ids4."AspNetUsers" ADD COLUMN IF NOT EXISTS "PasswordChangedAt" timestamptz NULL;
COMMIT;

-- 3) DOĞRULAMA --------------------------------------------------------------
SELECT column_name, data_type, is_nullable FROM information_schema.columns
WHERE table_schema = 'ids4' AND table_name = 'AspNetUsers' AND column_name = 'PasswordChangedAt';
-- PasswordChangedAt | timestamp with time zone | YES

SELECT count(*) AS dolu FROM ids4."AspNetUsers" WHERE "PasswordChangedAt" IS NOT NULL;   -- 0

-- GERİ ALMA (yalnızca gerekirse) ---------------------------------------------
-- Önce bu kolonu kullanan IdS kodu geri alınıp yayınlanmalı; yoksa kullanıcı sorguları hata verir.
-- BEGIN;
-- ALTER TABLE ids4."AspNetUsers" DROP COLUMN IF EXISTS "PasswordChangedAt";
-- COMMIT;
