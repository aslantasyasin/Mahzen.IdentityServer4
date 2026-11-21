-- LoginThrottle: başarısız şifre denemesi sayaçları (LoginProtectionService).
-- Çalıştırma sırası: 1) kontrol  2) oluşturma  3) doğrulama.  Geri alma en altta.
-- IdS kodu bu tablo olmadan da başlar; tablo yokken her denemede
-- "login_protection_store_failed" Warning logu düşer, giriş etkilenmez.

-- 1) KONTROL (salt okunur) ---------------------------------------------------
SELECT to_regclass('ids4."LoginThrottle"') AS mevcut_tablo;   -- NULL beklenir

-- 2) OLUŞTURMA (tek transaction, tekrar çalıştırılabilir) ---------------------
BEGIN;

CREATE TABLE IF NOT EXISTS ids4."LoginThrottle" (
    "UserId"      varchar(64)  NOT NULL,
    "Scope"       varchar(160) NOT NULL,
    "WindowStart" timestamptz  NOT NULL,
    "Failures"    integer      NOT NULL DEFAULT 0,
    CONSTRAINT "PK_LoginThrottle" PRIMARY KEY ("UserId", "Scope", "WindowStart")
);

CREATE INDEX IF NOT EXISTS "IX_LoginThrottle_WindowStart"
    ON ids4."LoginThrottle" ("WindowStart");

COMMIT;

-- 3) DOĞRULAMA --------------------------------------------------------------
-- 3a) Tablo ve indeksler
SELECT indexname FROM pg_indexes
WHERE schemaname = 'ids4' AND tablename = 'LoginThrottle'
ORDER BY indexname;                                  -- IX_LoginThrottle_WindowStart, PK_LoginThrottle

-- 3b) Uygulamanın kullandığı artırma sorgusu; iz bırakmaz (ROLLBACK).
BEGIN;
INSERT INTO ids4."LoginThrottle" ("UserId", "Scope", "WindowStart", "Failures")
VALUES ('__dogrulama__', 'source:test', '2026-01-01T00:00:00Z', 1)
ON CONFLICT ("UserId", "Scope", "WindowStart")
DO UPDATE SET "Failures" = ids4."LoginThrottle"."Failures" + 1
RETURNING "Failures";                                -- 1
INSERT INTO ids4."LoginThrottle" ("UserId", "Scope", "WindowStart", "Failures")
VALUES ('__dogrulama__', 'source:test', '2026-01-01T00:00:00Z', 1)
ON CONFLICT ("UserId", "Scope", "WindowStart")
DO UPDATE SET "Failures" = ids4."LoginThrottle"."Failures" + 1
RETURNING "Failures";                                -- 2
ROLLBACK;

SELECT count(*) AS satir FROM ids4."LoginThrottle" WHERE "UserId" = '__dogrulama__';  -- 0

-- GERİ ALMA (yalnızca gerekirse) ---------------------------------------------
-- Önce appsettings'te LoginProtection:Mode = "Off" yapılıp yayınlanmalı.
-- BEGIN;
-- DROP TABLE IF EXISTS ids4."LoginThrottle";
-- COMMIT;
