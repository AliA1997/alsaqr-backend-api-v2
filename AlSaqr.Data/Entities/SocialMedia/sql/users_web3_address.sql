-- Web3 login support (schema: alsaqr-2026)

alter table "alsaqr-2026".users
    add column if not exists web3_address varchar;

-- A wallet address identifies a single account (like email).
create unique index if not exists users_web3_address_key
    on "alsaqr-2026".users (web3_address)
    where web3_address is not null;
