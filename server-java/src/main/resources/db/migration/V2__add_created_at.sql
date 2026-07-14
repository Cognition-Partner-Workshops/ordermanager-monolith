-- V2__add_created_at.sql
-- Add the created_at audit column present in the .NET EF Core schema
-- (Customer.CreatedAt, Product.CreatedAt) so Java responses expose `createdAt`
-- exactly like the legacy backend. Values are populated by the JPA entities on
-- insert (default LocalDateTime.now()); the column is nullable for existing rows.

ALTER TABLE customers ADD COLUMN created_at TIMESTAMP;
ALTER TABLE products  ADD COLUMN created_at TIMESTAMP;
