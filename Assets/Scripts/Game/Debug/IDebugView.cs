namespace DarkFantasyMerchant.Game
{
    /// <summary>The content of a tab of the debug panel.</summary>
    public interface IDebugView
    {
        /// <summary>The tab is shown: the view writes itself and follows what it shows.</summary>
        void Show();

        /// <summary>The tab is hidden: a hidden view costs nothing.</summary>
        void Hide();

        /// <summary>The record has new days: the view writes itself again. Called at most once a frame, while shown.</summary>
        void Refresh();
    }
}
