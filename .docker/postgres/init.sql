-- 00-init.sql
-- Usuario y base de datos para Keycloak
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'keycloak') THEN
    CREATE USER keycloak WITH PASSWORD 'keycloak';
  END IF;
END
$$;

SELECT 'CREATE DATABASE keycloak OWNER keycloak'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak')
\gexec

-- Usuario y base de datos para la API (Sample)
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app_user') THEN
    CREATE USER app_user WITH PASSWORD 'sample';
  END IF;
END
$$;

SELECT 'CREATE DATABASE sample_db OWNER app_user'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'sample_db')
\gexec