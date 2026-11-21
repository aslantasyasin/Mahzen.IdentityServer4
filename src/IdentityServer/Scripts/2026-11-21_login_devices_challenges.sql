-- Giriş koruması: güvenilen cihazlar, e-posta kodu akışları, bildirim kayıtları.
-- Önce 2026-11-21_login_throttle.sql çalıştırılmış olmalı.
-- Çalıştırma sırası: 1) kontrol  2) oluşturma  3) doğrulama.  Geri alma en altta.

-- 1) KONTROL (salt okunur) ---------------------------------------------------
SELECT to_regclass('ids4."LoginThrottle"')  AS throttle_tablosu,   -- dolu olmalı
       to_regclass('ids4."LoginDevice"')    AS cihaz_tablosu,      -- NULL beklenir
       to_regclass('ids4."LoginChallenge"') AS kod_tablosu,        -- NULL beklenir
       to_regclass('ids4."LoginNotice"')    AS bildirim_tablosu;   -- NULL beklenir

-- 2) OLUŞTURMA (tek transaction, tekrar çalıştırılabilir) ---------------------
BEGIN;

CREATE TABLE IF NOT EXISTS ids4."LoginDevice" (
    "Id"           bigserial    NOT NULL,
    "UserId"       varchar(64)  NOT NULL,
    "TokenHash"    char(64)     NOT NULL,
    "CreatedAt"    timestamptz  NOT NULL,
    "LastUsedAt"   timestamptz  NOT NULL,
    "ExpiresAt"    timestamptz  NOT NULL,
    "RevokedAt"    timestamptz  NULL,
    "RevokeReason" varchar(32)  NULL,
    CONSTRAINT "PK_LoginDevice" PRIMARY KEY ("Id"),
    CONSTRAINT "UQ_LoginDevice_TokenHash" UNIQUE ("TokenHash")
);
CREATE INDEX IF NOT EXISTS "IX_LoginDevice_UserId" ON ids4."LoginDevice" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_LoginDevice_ExpiresAt" ON ids4."LoginDevice" ("ExpiresAt");

CREATE TABLE IF NOT EXISTS ids4."LoginChallenge" (
    "Id"          uuid         NOT NULL,
    "UserId"      varchar(64)  NOT NULL,
    "CodeHash"    char(64)     NOT NULL,
    "Attempts"    integer      NOT NULL DEFAULT 0,
    "MaxAttempts" integer      NOT NULL,
    "CreatedAt"   timestamptz  NOT NULL,
    "ExpiresAt"   timestamptz  NOT NULL,
    "LastSentAt"  timestamptz  NOT NULL,
    "UsedAt"      timestamptz  NULL,
    CONSTRAINT "PK_LoginChallenge" PRIMARY KEY ("Id")
);
CREATE INDEX IF NOT EXISTS "IX_LoginChallenge_Open" ON ids4."LoginChallenge" ("UserId", "CreatedAt") WHERE "UsedAt" IS NULL;
CREATE INDEX IF NOT EXISTS "IX_LoginChallenge_ExpiresAt" ON ids4."LoginChallenge" ("ExpiresAt");

CREATE TABLE IF NOT EXISTS ids4."LoginNotice" (
    "UserId" varchar(64) NOT NULL,
    "Kind"   varchar(32) NOT NULL,
    "SentAt" timestamptz NOT NULL,
    CONSTRAINT "PK_LoginNotice" PRIMARY KEY ("UserId", "Kind")
);

COMMIT;

-- 3) DOĞRULAMA --------------------------------------------------------------
SELECT tablename, indexname FROM pg_indexes
WHERE schemaname = 'ids4' AND tablename IN ('LoginDevice', 'LoginChallenge', 'LoginNotice')
ORDER BY tablename, indexname;
-- Beklenen: LoginChallenge (IX_LoginChallenge_ExpiresAt, IX_LoginChallenge_Open, PK_LoginChallenge),
--           LoginDevice (IX_LoginDevice_ExpiresAt, IX_LoginDevice_UserId, PK_LoginDevice, UQ_LoginDevice_TokenHash),
--           LoginNotice (PK_LoginNotice)

-- Uygulamanın koşullu yazımları; iz bırakmaz (ROLLBACK).
BEGIN;
INSERT INTO ids4."LoginChallenge" ("Id", "UserId", "CodeHash", "Attempts", "MaxAttempts", "CreatedAt", "ExpiresAt", "LastSentAt")
VALUES ('00000000-0000-0000-0000-000000000001', '__dogrulama__', repeat('a', 64), 0, 1, now(), now() + interval '5 minutes', now());
UPDATE ids4."LoginChallenge" SET "Attempts" = "Attempts" + 1
WHERE "Id" = '00000000-0000-0000-0000-000000000001' AND "UsedAt" IS NULL AND "ExpiresAt" > now() AND "Attempts" < "MaxAttempts";  -- UPDATE 1
UPDATE ids4."LoginChallenge" SET "Attempts" = "Attempts" + 1
WHERE "Id" = '00000000-0000-0000-0000-000000000001' AND "UsedAt" IS NULL AND "ExpiresAt" > now() AND "Attempts" < "MaxAttempts";  -- UPDATE 0
INSERT INTO ids4."LoginNotice" ("UserId", "Kind", "SentAt") VALUES ('__dogrulama__', 'attack', now())
ON CONFLICT ("UserId", "Kind") DO UPDATE SET "SentAt" = now() WHERE ids4."LoginNotice"."SentAt" <= now() - interval '24 hours'
RETURNING 1;                                                                                                                    -- 1 satır
INSERT INTO ids4."LoginNotice" ("UserId", "Kind", "SentAt") VALUES ('__dogrulama__', 'attack', now())
ON CONFLICT ("UserId", "Kind") DO UPDATE SET "SentAt" = now() WHERE ids4."LoginNotice"."SentAt" <= now() - interval '24 hours'
RETURNING 1;                                                                                                                    -- 0 satır
ROLLBACK;

SELECT (SELECT count(*) FROM ids4."LoginChallenge" WHERE "UserId" = '__dogrulama__')
     + (SELECT count(*) FROM ids4."LoginNotice" WHERE "UserId" = '__dogrulama__') AS iz;   -- 0

-- GERİ ALMA (yalnızca gerekirse) ---------------------------------------------
-- Önce appsettings'te LoginProtection:Mode = "Off" yapılıp yayınlanmalı.
-- BEGIN;
-- DROP TABLE IF EXISTS ids4."LoginNotice";
-- DROP TABLE IF EXISTS ids4."LoginChallenge";
-- DROP TABLE IF EXISTS ids4."LoginDevice";
-- COMMIT;
