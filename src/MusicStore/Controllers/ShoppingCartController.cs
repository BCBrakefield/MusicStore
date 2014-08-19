﻿using Microsoft.AspNet.Mvc;
using MusicStore.Models;
using MusicStore.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.DependencyInjection;

namespace MusicStore.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly MusicStoreContext db;

        public ShoppingCartController(MusicStoreContext context)
        {
            db = context;
        }

        //
        // GET: /ShoppingCart/

        public IActionResult Index()
        {
            var cart = ShoppingCart.GetCart(db, Context);

            // Set up our ViewModel
            var viewModel = new ShoppingCartViewModel
            {
                CartItems = DbHelper.GetCartItems(db, cart.GetCartId(Context)),
                CartTotal = DbHelper.GetCartTotal(db, cart.GetCartId(this.Context))
            };

            // Return the view
            return View(viewModel);
        }

        //
        // GET: /ShoppingCart/AddToCart/5

        public async Task<IActionResult> AddToCart(int id)
        {
            // Retrieve the album from the database
            var album = await db.Albums.SingleOrDefaultAsync(alb => alb.AlbumId == id);

            if (album == null)
            {
                return HttpNotFound();
            }

            // Add it to the shopping cart
            var cart = ShoppingCart.GetCart(db, Context);

            await cart.AddToCart(album);
            await db.SaveChangesAsync();

            // Go back to the main store page for more shopping
            return RedirectToAction("Index");
        }

        //
        // AJAX: /ShoppingCart/RemoveFromCart/5
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var formParameters = await Context.Request.GetFormAsync();
            var requestVerification = formParameters["RequestVerificationToken"];
            string cookieToken = null;
            string formToken = null;

            if (!string.IsNullOrWhiteSpace(requestVerification))
            {
                var tokens = requestVerification.Split(':');

                if (tokens != null && tokens.Length == 2)
                {
                    cookieToken = tokens[0];
                    formToken = tokens[1];
                }
            }

            var antiForgery = Context.RequestServices.GetService<AntiForgery>();
            antiForgery.Validate(Context, new AntiForgeryTokenSet(formToken, cookieToken));

            // Retrieve the current user's shopping cart
            var cart = ShoppingCart.GetCart(db, Context);

            // Get the name of the album to display confirmation
            // TODO [EF] Turn into one query once query of related data is enabled
            int albumId = db.CartItems.Single(item => item.CartItemId == id).AlbumId;
            string albumName = db.Albums.Single(a => a.AlbumId == albumId).Title;

            // Remove from cart
            int itemCount = await cart.RemoveFromCartAsync(id);

            await db.SaveChangesAsync();

            string removed = (itemCount > 0) ? " 1 copy of " : string.Empty;

            // Display the confirmation message

            var results = new ShoppingCartRemoveViewModel
            {
                Message = removed + albumName +
                    " has been removed from your shopping cart.",
                CartTotal = DbHelper.GetCartTotal(db, cart.GetCartId(this.Context)),
                CartCount = cart.GetCount(),
                ItemCount = itemCount,
                DeleteId = id
            };

            return Json(results);
        }
    }
}