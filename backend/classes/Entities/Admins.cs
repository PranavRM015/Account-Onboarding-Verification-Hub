using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admins
{
    public class Admin
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdminId { get; set; }
        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = string.Empty;
        private Admin() { }
        public Admin(string username, string passwordHash)
        {
            this.Username = username;
            this.PasswordHash = passwordHash;
        }
    }
    public class AdminConfiguration : IEntityTypeConfiguration<Admin>
    {
        public void Configure(EntityTypeBuilder<Admin> builder)
        {
            builder.HasAlternateKey(a => a.Username);
        }
    }
    public class AdminValidator : AbstractValidator<Admin>
    {
        public AdminValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .WithMessage("Username is required.")
                .MaximumLength(100)
                .WithMessage("Username cannot exceed 100 characters.");

            RuleFor(x => x.PasswordHash)
                .NotEmpty()
                .WithMessage("Password hash is required.")
                .MaximumLength(200)
                .WithMessage("Password hash cannot exceed 200 characters.");
        }
    }
}
