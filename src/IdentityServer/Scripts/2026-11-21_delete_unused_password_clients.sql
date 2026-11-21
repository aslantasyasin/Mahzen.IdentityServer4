-- Kullanılmayan password grant client'ları: 'job-tracking-ui' (secret'sız) ve 'client' (seed secret'ı "secret").
-- IsClientAllowed kuralı gereği Admin tipindeki hesaplar bu client'larla token alabiliyor.
-- Hiçbir repoda kullanıcıları yok; client'lar, alt kayıtları ve verdikleri grant'ler silinir.
-- Silmeden önce tüm satırlar ids4.deleted_clients_backup tablosuna JSON olarak yedeklenir.

-- 1) KONTROL (salt okunur) ---------------------------------------------------
SELECT "Id", "ClientId", "Enabled", "RequireClientSecret" FROM ids4."Clients" WHERE "ClientId" IN ('job-tracking-ui', 'client');
SELECT "ClientId", "Type", count(*), max("CreationTime") AS son_grant
FROM ids4."PersistedGrants" WHERE "ClientId" IN ('job-tracking-ui', 'client') GROUP BY "ClientId", "Type";
SELECT c."ClientId", s."Value" = 'K7gNU3sdo+OL0wNhqoVWhr3g6s1xYv72ol/pe/Unols=' AS secret_duz_secret_mi
FROM ids4."ClientSecrets" s JOIN ids4."Clients" c ON c."Id" = s."ClientId" WHERE c."ClientId" = 'client';

-- 2) YEDEK + SİLME (tek transaction, tekrar çalıştırılabilir) -----------------
BEGIN;

CREATE TABLE IF NOT EXISTS ids4.deleted_clients_backup (
    "SourceTable" text        NOT NULL,
    "Row"         jsonb       NOT NULL,
    "BackedUpAt"  timestamptz NOT NULL DEFAULT now()
);

CREATE TEMP TABLE target_client ON COMMIT DROP AS
SELECT "Id" FROM ids4."Clients" WHERE "ClientId" IN ('job-tracking-ui', 'client');

INSERT INTO ids4.deleted_clients_backup ("SourceTable", "Row")
SELECT 'Clients', to_jsonb(c) FROM ids4."Clients" c WHERE c."Id" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientClaims', to_jsonb(t) FROM ids4."ClientClaims" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientCorsOrigins', to_jsonb(t) FROM ids4."ClientCorsOrigins" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientGrantTypes', to_jsonb(t) FROM ids4."ClientGrantTypes" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientIdPRestrictions', to_jsonb(t) FROM ids4."ClientIdPRestrictions" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientPostLogoutRedirectUris', to_jsonb(t) FROM ids4."ClientPostLogoutRedirectUris" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientProperties', to_jsonb(t) FROM ids4."ClientProperties" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientRedirectUris', to_jsonb(t) FROM ids4."ClientRedirectUris" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientScopes', to_jsonb(t) FROM ids4."ClientScopes" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client)
UNION ALL SELECT 'ClientSecrets', to_jsonb(t) FROM ids4."ClientSecrets" t WHERE t."ClientId" IN (SELECT "Id" FROM target_client);

DELETE FROM ids4."ClientClaims"                 WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientCorsOrigins"            WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientGrantTypes"             WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientIdPRestrictions"        WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientPostLogoutRedirectUris" WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientProperties"             WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientRedirectUris"           WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientScopes"                 WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."ClientSecrets"                WHERE "ClientId" IN (SELECT "Id" FROM target_client);
DELETE FROM ids4."Clients"                      WHERE "Id"       IN (SELECT "Id" FROM target_client);

DELETE FROM ids4."PersistedGrants" WHERE "ClientId" IN ('job-tracking-ui', 'client');

COMMIT;

-- 3) DOĞRULAMA --------------------------------------------------------------
SELECT count(*) AS kalan_client FROM ids4."Clients" WHERE "ClientId" IN ('job-tracking-ui', 'client');          -- 0
SELECT count(*) AS kalan_grant  FROM ids4."PersistedGrants" WHERE "ClientId" IN ('job-tracking-ui', 'client');  -- 0
SELECT "SourceTable", count(*) FROM ids4.deleted_clients_backup GROUP BY "SourceTable";
SELECT "ClientId" FROM ids4."Clients" ORDER BY "ClientId";   -- B2B, B2C, mahzen-mobile-bff, order-client
-- Silinen refresh token'ların yedeği alınmaz; geri alma sonrası kullanıcılar yeniden giriş yapar.

-- GERİ ALMA -----------------------------------------------------------------
-- Clients satırları "Id" ile geri yazılır, alt tablolar aynı "ClientId" (int) ile bağlanır.
-- BEGIN;
-- INSERT INTO ids4."Clients" SELECT (jsonb_populate_record(NULL::ids4."Clients", "Row")).* FROM ids4.deleted_clients_backup WHERE "SourceTable" = 'Clients';
-- INSERT INTO ids4."ClientGrantTypes" SELECT (jsonb_populate_record(NULL::ids4."ClientGrantTypes", "Row")).* FROM ids4.deleted_clients_backup WHERE "SourceTable" = 'ClientGrantTypes';
-- INSERT INTO ids4."ClientScopes" SELECT (jsonb_populate_record(NULL::ids4."ClientScopes", "Row")).* FROM ids4.deleted_clients_backup WHERE "SourceTable" = 'ClientScopes';
-- (diğer alt tablolar aynı kalıpla; yedekte satırı olmayanlar atlanır)
-- ClientSecrets YEDEKTEN GERİ YAZILMAZ: 'client' secret'ı herkesçe bilinen "secret".
-- 'client' geri alınırsa yeni ve güçlü bir secret üretilip yalnız onun SHA256 base64 özeti eklenir.
-- Geri almadan önce: SELECT is_identity, identity_generation FROM information_schema.columns
--   WHERE table_schema = 'ids4' AND table_name = 'Clients' AND column_name = 'Id';
--   identity_generation = ALWAYS ise Clients INSERT'üne OVERRIDING SYSTEM VALUE eklenir.
-- COMMIT;

-- YEDEK TEMİZLİĞİ: 2026-12-21'e kadar sorun çıkmazsa ------------------------
-- DROP TABLE ids4.deleted_clients_backup;
