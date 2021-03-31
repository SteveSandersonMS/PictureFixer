using System;
using PictureFixer.Shared;

namespace PictureFixer.Server.Data
{
    public class SeedData
    {
        public static void Initialize(EditableImageDbContext db)
        {
            var rng = new Random();
            var images = new[]
            {
                new EditableImage
                {
                    Name = "Happy kitten",
                    Description = "A very cheery kitten smiles for the camera",
                    CdnLocation = "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8f/Cute-kittens-12929201-1600-1200.jpg/1200px-Cute-kittens-12929201-1600-1200.jpg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Sleepy puppy",
                    Description = "A puppy getting ready for a great nap",
                    CdnLocation = "https://upload.wikimedia.org/wikipedia/commons/c/c7/Puppy_on_Halong_Bay.jpg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Golden Gate Bridge",
                    Description = "San Francisco's iconic bridge on a sunny day",
                    CdnLocation = "/samples/golden-gate.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Food vendor",
                    Description = "A city food vendor at night",
                    CdnLocation = "/samples/shop-at-night.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Grey kitten",
                    Description = "A ferocious predator preparing to strike",
                    CdnLocation = "/samples/grey-kitten.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Football",
                    Description = "A game of football in progress",
                    CdnLocation = "/samples/football.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Tennis",
                    Description = "Ramos-Viñolas about to hit the ball",
                    CdnLocation = "/samples/tennis.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Italian ingredients",
                    Description = "Tomato, basil, and spaghetti, ready to cook",
                    CdnLocation = "/samples/italian-food.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                },
                new EditableImage
                {
                    Name = "Horse riding",
                    Description = "A typical scene from Unterwasser, Switzerland",
                    CdnLocation = "/samples/horse-riding.jpeg",
                    CreatedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                    ModifiedAt = DateTime.Now.AddDays(-1 * rng.Next(30)),
                }
            };

            foreach (var i in images)
            {
                db.Images.Add(i);
            }
            db.SaveChanges();
        }
    }
}
