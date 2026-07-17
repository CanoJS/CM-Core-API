alter table medical.user_profiles
    add column first_name varchar(100),
    add column last_name varchar(100);

alter table medical.user_profiles
    drop column full_name,
    alter column first_name set not null,
    alter column last_name set not null;

create index ix_doctors_specialty_id
    on medical.doctors (specialty_id);

create or replace function medical.handle_new_auth_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    provided_first_name text := btrim(coalesce(new.raw_user_meta_data->>'first_name', ''));
    provided_last_name text := btrim(coalesce(new.raw_user_meta_data->>'last_name', ''));
begin
    if provided_first_name = '' or length(provided_first_name) > 100 then
        raise exception using
            errcode = '23514',
            message = 'First name is required and cannot exceed 100 characters.';
    end if;

    if provided_last_name = '' or length(provided_last_name) > 100 then
        raise exception using
            errcode = '23514',
            message = 'Last name is required and cannot exceed 100 characters.';
    end if;

    if new.email is null or btrim(new.email) = '' or length(btrim(new.email)) > 320 then
        raise exception using
            errcode = '23514',
            message = 'Email is required and cannot exceed 320 characters.';
    end if;

    insert into medical.user_profiles (
        id,
        first_name,
        last_name,
        email,
        role,
        active)
    values (
        new.id,
        provided_first_name,
        provided_last_name,
        lower(btrim(new.email)),
        'PATIENT',
        true);

    return new;
end;
$$;

revoke execute on function medical.handle_new_auth_user() from anon, authenticated, public;

drop trigger if exists on_auth_user_created on auth.users;

create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function medical.handle_new_auth_user();

do $$
begin
    if to_regprocedure('public.rls_auto_enable()') is not null then
        execute
            'revoke execute on function public.rls_auto_enable() from anon, authenticated, public';
    end if;
end;
$$;

comment on function medical.handle_new_auth_user() is
    'Creates the private application profile for a new Supabase Auth user with PATIENT role.';
