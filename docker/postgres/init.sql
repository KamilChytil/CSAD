-- Create application user (services connect as this user)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'fairbank_app') THEN
        CREATE ROLE fairbank_app WITH LOGIN PASSWORD 'fairbank_app_2026';
    END IF;
END
$$;

-- Create separate schemas for each microservice
CREATE SCHEMA IF NOT EXISTS identity_service;
CREATE SCHEMA IF NOT EXISTS accounts_service;
CREATE SCHEMA IF NOT EXISTS payments_service;
CREATE SCHEMA IF NOT EXISTS chat_service;
CREATE SCHEMA IF NOT EXISTS products_service;

-- Grant permissions
GRANT CONNECT ON DATABASE fairbank TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA identity_service TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA accounts_service TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA payments_service TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA chat_service TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA products_service TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA accounts_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA payments_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA chat_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA products_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity_service GRANT ALL ON SEQUENCES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA accounts_service GRANT ALL ON SEQUENCES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA payments_service GRANT ALL ON SEQUENCES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA chat_service GRANT ALL ON SEQUENCES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA products_service GRANT ALL ON SEQUENCES TO fairbank_app;
