insert into medical.specialties (id, name)
values
    ('10000000-0000-0000-0000-000000000001', 'Pediatría'),
    ('10000000-0000-0000-0000-000000000002', 'Cardiología')
on conflict do nothing;
