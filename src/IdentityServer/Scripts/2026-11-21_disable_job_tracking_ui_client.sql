-- job-tracking-ui: secret istemeyen password grant client'ı. IsClientAllowed kuralı gereği
-- Admin tipindeki hesaplar bu client'la secret olmadan token alabiliyor; IdS'e doğrudan
-- ulaşan biri BFF hız sınırlarını atlayarak admin şifresi deneyebilir.
-- Silinmez, devre dışı bırakılır (geri alma tek UPDATE).

-- 1) KONTROL (salt okunur) ---------------------------------------------------
SELECT "Id", "ClientId", "Enabled", "RequireClientSecret" FROM ids4."Clients" WHERE "ClientId" = 'job-tracking-ui';

-- 2) DEVRE DIŞI BIRAKMA (tek transaction, tekrar çalıştırılabilir) -------------
BEGIN;
UPDATE ids4."Clients" SET "Enabled" = false WHERE "ClientId" = 'job-tracking-ui' AND "Enabled" = true;
COMMIT;

-- 3) DOĞRULAMA --------------------------------------------------------------
SELECT "ClientId", "Enabled" FROM ids4."Clients" WHERE "ClientId" = 'job-tracking-ui';   -- Enabled = false
-- IdS client'ları önbelleğe alıyorsa etki pod yeniden başlayınca kesinleşir.

-- GERİ ALMA -----------------------------------------------------------------
-- BEGIN;
-- UPDATE ids4."Clients" SET "Enabled" = true WHERE "ClientId" = 'job-tracking-ui';
-- COMMIT;
