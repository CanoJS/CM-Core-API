create schema if not exists medical;

revoke all on schema medical from anon, authenticated;

create table medical.user_profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    full_name varchar(200) not null,
    email varchar(320) not null,
    role varchar(20) not null,
    active boolean not null default true,
    constraint ck_user_profiles_role check (role in ('PATIENT', 'DOCTOR', 'ADMIN'))
);

create unique index ux_user_profiles_email
    on medical.user_profiles (lower(email));

create table medical.specialties (
    id uuid primary key,
    name varchar(120) not null,
    active boolean not null default true
);

create unique index ux_specialties_name
    on medical.specialties (lower(name));

create table medical.doctors (
    id uuid primary key,
    user_id uuid not null unique references medical.user_profiles(id) on delete restrict,
    specialty_id uuid not null references medical.specialties(id) on delete restrict,
    active boolean not null default true
);

create table medical.appointments (
    id uuid primary key,
    patient_id uuid not null references medical.user_profiles(id) on delete restrict,
    doctor_id uuid not null references medical.doctors(id) on delete restrict,
    starts_at timestamptz not null,
    reason varchar(500) not null,
    status varchar(20) not null,
    medical_note varchar(4000),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint ck_appointments_status
        check (status in ('SCHEDULED', 'ATTENDED', 'CANCELLED')),
    constraint ck_appointments_half_hour_boundary
        check (
            extract(minute from starts_at)::integer % 30 = 0
            and extract(second from starts_at) = 0
        ),
    constraint ck_appointments_clinic_hours
        check (
            extract(isodow from starts_at at time zone 'America/Mexico_City') between 1 and 5
            and (starts_at at time zone 'America/Mexico_City')::time >= time '08:00'
            and (starts_at at time zone 'America/Mexico_City')::time < time '18:00'
        ),
    constraint ck_appointments_medical_note
        check (
            (status = 'ATTENDED' and medical_note is not null)
            or (status <> 'ATTENDED' and medical_note is null)
        )
);

create unique index ux_appointments_doctor_slot_scheduled
    on medical.appointments (doctor_id, starts_at)
    where status = 'SCHEDULED';

create index ix_appointments_patient_starts_at
    on medical.appointments (patient_id, starts_at);

create index ix_appointments_doctor_starts_at
    on medical.appointments (doctor_id, starts_at);

create table medical.idempotency_requests (
    id uuid primary key,
    user_id uuid not null references medical.user_profiles(id) on delete cascade,
    operation varchar(100) not null,
    idempotency_key varchar(200) not null,
    request_hash varchar(128) not null,
    response_status integer,
    response_body jsonb,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    constraint ux_idempotency_user_operation_key
        unique (user_id, operation, idempotency_key)
);

alter table medical.user_profiles enable row level security;
alter table medical.specialties enable row level security;
alter table medical.doctors enable row level security;
alter table medical.appointments enable row level security;
alter table medical.idempotency_requests enable row level security;

revoke all on all tables in schema medical from anon, authenticated;
revoke all on all sequences in schema medical from anon, authenticated;

create or replace function medical.custom_access_token_hook(event jsonb)
returns jsonb
language plpgsql
stable
set search_path = ''
as $$
declare
    claims jsonb;
    application_role text;
begin
    select profile.role
      into application_role
      from medical.user_profiles as profile
     where profile.id = (event->>'user_id')::uuid
       and profile.active;

    claims := event->'claims';
    claims := jsonb_set(
        claims,
        '{user_role}',
        coalesce(to_jsonb(application_role), 'null'::jsonb));
    event := jsonb_set(event, '{claims}', claims);
    return event;
end;
$$;

grant usage on schema medical to supabase_auth_admin;
grant select on medical.user_profiles to supabase_auth_admin;
grant execute on function medical.custom_access_token_hook(jsonb) to supabase_auth_admin;
revoke execute on function medical.custom_access_token_hook(jsonb) from anon, authenticated, public;

create policy "Auth can read active application roles"
    on medical.user_profiles
    for select
    to supabase_auth_admin
    using (active);

comment on schema medical is
    'Private business schema. Web and mobile clients must use the ASP.NET Core API.';
