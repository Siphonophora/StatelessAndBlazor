using Stateless;
using Stateless.Graph;
using StatelessBlazor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatelessBlazor.Models
{
    public class ShoppingCart
    {
        private readonly StateMachine<ShoppingCartState, ShoppingCartTrigger> stateMachine;

        public ShoppingCart()
        {
            // This constructor for the state machine allows us to store the actual state outside of
            // the state machine, in the State property, where we can persist it to the database.
            stateMachine = new(() => State, (state) => State = state);

            stateMachine.Configure(ShoppingCartState.Draft)
                .PermitReentry(ShoppingCartTrigger.AddItem)
                .PermitReentryIf(ShoppingCartTrigger.RemoveItem, CartHasItems)
                .Permit(ShoppingCartTrigger.DeleteCart, ShoppingCartState.Deleted)
                .PermitIf(ShoppingCartTrigger.PurchaseCart, ShoppingCartState.Purchased, CartHasItems)
                .Permit(ShoppingCartTrigger.SaveCart, ShoppingCartState.Saved);

            stateMachine.Configure(ShoppingCartState.Saved)
                .Permit(ShoppingCartTrigger.DeleteCart, ShoppingCartState.Deleted)
                .Permit(ShoppingCartTrigger.EditCart, ShoppingCartState.Draft)
                .PermitIf(ShoppingCartTrigger.PurchaseCart, ShoppingCartState.Purchased, CartHasItems);

            // Note, we can often make the declaration of the state machine shorter and easier to
            // manage with code like this, which adds the AddNote ability to all states in two lines
            // of code, regardless of the total number of states.
            Enum.GetValues<ShoppingCartState>().ToList()
                .ForEach(x => stateMachine.Configure(x).PermitReentry(ShoppingCartTrigger.AddNote));

            // Uncomment to get a graph string which can be viewed at http://www.webgraphviz.com/
            //// string graph = UmlDotGraph.Format(stateMachine.GetInfo());
        }

        public ShoppingCartState State { get; private set; } = ShoppingCartState.Draft;

        public int ItemCount { get; private set; }

        public List<string> Log { get; } = new();

        /// <summary>
        /// Public utility class the UI can use to test what actions are permitted
        /// </summary>
        public bool CanFire(ShoppingCartTrigger trigger) => stateMachine.CanFire(trigger);

        public void AddItem() => Fire(ShoppingCartTrigger.AddItem, () => ItemCount++);

        public void RemoveItem() => Fire(ShoppingCartTrigger.RemoveItem, () => ItemCount--);

        public void PurchaseCart() => Fire(ShoppingCartTrigger.PurchaseCart);

        public void SaveCart() => Fire(ShoppingCartTrigger.SaveCart);

        public void EditCart() => Fire(ShoppingCartTrigger.EditCart);

        public void DeleteCart() => Fire(ShoppingCartTrigger.DeleteCart);

        public void AddNote() => Fire(ShoppingCartTrigger.AddNote);

        private bool CartHasItems() => ItemCount > 0;

        /// <summary>
        /// Fire takes an exlcusive lock on <see cref="stateMachine"/> so we only have one
        /// transition happening at a time.
        /// </summary>
        /// <param name="trigger">Trigger to fire.</param>
        /// <param name="postFireAction">Business logic to run if we are able to fire the trigger.</param>
        private void Fire(ShoppingCartTrigger trigger, Action? postFireAction = null)
        {
            lock (stateMachine)
            {
                // The state machine will throw an exception if you call Fire when the trigger is
                // not allowed.
                if (stateMachine.CanFire(trigger))
                {
                    var initialState = State;
                    stateMachine.Fire(trigger);
                    postFireAction?.Invoke();
                    Log.Add(
                        $"{DateTime.Now} - " +
                        $"In state {initialState}. " +
                        $"Fired trigger {trigger}. " +
                        $"{(initialState != State ? $"Transitioned to {State}" : string.Empty)}");
                }
            }
        }
    }
}
