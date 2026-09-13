namespace RichCoast.Core
{
    /// <summary>
    /// The one place the privacy policy's location is written down.
    /// <para><see cref="Url"/> is DELIBERATELY empty until the policy has a host. An empty URL makes
    /// the consent screen show the short notice alone, which is honest; a placeholder URL would ship
    /// a dead link in a compliance surface, which is worse than no link at all.</para>
    /// <para>The policy text itself lives in <c>docs/privacy-policy.md</c>. Host it, put the address
    /// here and in the Play Console listing, and the two stay in step because there is only one of
    /// them.</para>
    /// </summary>
    public static class PrivacyPolicy
    {
        /// <summary>Where the hosted policy lives. Empty until it is hosted — see the class remarks.</summary>
        public const string Url = "";

        public static bool IsHosted => !string.IsNullOrEmpty(Url);

        /// <summary>The line under the consent buttons: the address when there is one, the gist when there is not.</summary>
        public static string ShortNotice =>
            IsHosted ? Url : "Anonymous and aggregated. Never sold, never shared.";
    }
}
