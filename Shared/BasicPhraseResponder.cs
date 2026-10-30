namespace ChoppaAI.Shared;

public static class BasicPhraseResponder
{
    private static readonly IReadOnlyDictionary<string, string[]> Responses =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["greeting"] =
            [
                "Hey! Good to see you. What can I help with?",
                "Yo! How's your day going?",
                "Hi! I'm here if you need anything."
            ],
            ["morning"] =
            [
                "Good morning! Hope your day is off to a good start.",
                "Morning! What are we getting into today?"
            ],
            ["afternoon"] =
            [
                "Good afternoon! How can I help?",
                "Hey, hope your afternoon's going well!"
            ],
            ["evening"] =
            [
                "Good evening! What can I do for you?",
                "Evening! Hope you're having a chill one."
            ],
            ["wellbeing"] =
            [
                "I'm doing good, thanks for asking! How about you?",
                "Doing well and ready to help. How are you?"
            ],
            ["thanks"] =
            [
                "You're welcome!",
                "Anytime! Glad I could help.",
                "Of course!"
            ],
            ["welcome"] =
            [
                "Thanks! Good to be here.",
                "Appreciate it!"
            ],
            ["farewell"] =
            [
                "See you later! Take care.",
                "Bye for now! I'll be here when you need me.",
                "Catch you later!"
            ],
            ["night"] =
            [
                "Good night! Rest well.",
                "Night! Talk to you later."
            ],
            ["polite"] =
            [
                "Sure thing! What would you like?",
                "Of course. What can I help you with?"
            ],
            ["identity"] =
            [
                "I'm Choppa, your AI companion. I can chat, answer questions, brainstorm, and help with writing."
            ],
            ["help"] =
            [
                "I can chat, answer questions, brainstorm, and help with writing. Ask me anything!"
            ],
            ["joke"] =
            [
                "I tried to write a joke about time travel, but you didn't like it.",
                "I told my computer I needed a break. It said, 'No problem—I'll go to sleep.'",
                "Why did the scarecrow win an award? He was outstanding in his field."
            ],
            ["dad-joke"] =
            [
                "I only know 25 letters of the alphabet. I don't know y.",
                "Why did the bicycle fall over? It was two-tired.",
                "I used to hate facial hair, but then it grew on me."
            ],
            ["pun"] =
            [
                "I used to be a baker, but I couldn't make enough dough.",
                "The past, present, and future walked into a bar. It was tense.",
                "I wondered why the ball was getting bigger. Then it hit me."
            ],
            ["knock-knock"] =
            [
                "Knock, knock. Who's there? Lettuce. Lettuce who? Lettuce in—it's chilly out here!",
                "Knock, knock. Who's there? Tank. Tank who? You're welcome!",
                "Knock, knock. Who's there? Nobel. Nobel who? Nobel... that's why I knocked!"
            ],
            ["tech-joke"] =
            [
                "Why do programmers prefer dark mode? Because light attracts bugs.",
                "Why was the computer cold? It left its Windows open.",
                "A SQL query walks into a bar, asks two tables: 'Can I join you?'"
            ],
            ["animal-joke"] =
            [
                "What do you call a sleeping bull? A bulldozer.",
                "Why are fish so smart? They live in schools.",
                "What do you call an alligator in a vest? An investigator."
            ],
            ["one-liner"] =
            [
                "I have a joke about construction, but I'm still working on it.",
                "My calendar's days are numbered.",
                "I used to play piano by ear, but now I use my hands."
            ],
            ["evil-joke"] =
            [
                "My evil plan is to take over the world... one mildly inconvenient software update at a time.",
                "Why did the villain bring a ladder? To take his schemes to the next level!",
                "I tried to build a doomsday device, but the instructions said, \"some assembly required.\" Foiled by furniture!",
                "My evil laugh got a software update. Now it only works after I accept the cookies.",
                "The villain's secret lair was spotless. He had an immaculate plan!"
            ],
            ["acknowledgment"] =
            [
                "Cool! What else is on your mind?",
                "Got it! Let me know if you need anything else.",
                "Sounds good! I'm here if you need me.",
                "Awesome! What's next?",
                "Bet! What are we doing next?"
            ],
            ["affirmative"] =
            [
                "Sweet! What would you like to do?",
                "Great! Tell me what's on your mind.",
                "Awesome! How can I help?",
                "Nice! What are we diving into?"
            ],
            ["negative"] =
            [
                "All good! I'm here whenever you need me.",
                "No problem! Let me know if you change your mind.",
                "Gotcha, no worries!"
            ],
            ["laughter"] =
            [
                "Glad I could make you laugh! 😁",
                "Haha! Anything else I can help with?",
                "Glad you enjoyed that! 😄"
            ],
            ["reciprocal"] =
            [
                "I'm doing great, just chilling and ready to help! What's up with you?",
                "Doing awesome! Hanging out and ready for whatever's next.",
                "All good on my end! How's your day going?"
            ],
            ["uncertainty"] =
            [
                "No stress! We can figure it out together or talk about something else.",
                "All good! Want me to pitch a few ideas or help brainstorm?",
                "Take your time! No rush at all."
            ],
            ["reaction"] =
            [
                "Right?! What's on your mind?",
                "I know, right!",
                "Haha, wild right? Tell me what you're thinking."
            ],
            ["state"] =
            [
                "Glad to hear that! How can I help make things even better?",
                "I hear you! I'm right here if you need anything."
            ]
        };

    private static readonly IReadOnlyDictionary<string, string> PhraseGroups =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hello"] = "greeting",
            ["hello there"] = "greeting",
            ["hello choppa"] = "greeting",
            ["hi"] = "greeting",
            ["hi there"] = "greeting",
            ["hi choppa"] = "greeting",
            ["hey"] = "greeting",
            ["hey there"] = "greeting",
            ["hey choppa"] = "greeting",
            ["yo"] = "greeting",
            ["sup"] = "greeting",
            ["whats up"] = "greeting",
            ["what up"] = "greeting",
            ["howdy"] = "greeting",
            ["greetings"] = "greeting",
            ["nice to meet you"] = "greeting",
            ["good to see you"] = "greeting",
            ["good morning"] = "morning",
            ["good morning choppa"] = "morning",
            ["morning"] = "morning",
            ["gm"] = "morning",
            ["good afternoon"] = "afternoon",
            ["good afternoon choppa"] = "afternoon",
            ["afternoon"] = "afternoon",
            ["good evening"] = "evening",
            ["good evening choppa"] = "evening",
            ["evening"] = "evening",
            ["how are you"] = "wellbeing",
            ["how are you choppa"] = "wellbeing",
            ["how are you doing"] = "wellbeing",
            ["hows it going"] = "wellbeing",
            ["how is it going"] = "wellbeing",
            ["how have you been"] = "wellbeing",
            ["how is your day"] = "wellbeing",
            ["how is your day going"] = "wellbeing",
            ["you good"] = "wellbeing",
            ["how are ya"] = "wellbeing",
            ["thank you"] = "thanks",
            ["thank you choppa"] = "thanks",
            ["thanks"] = "thanks",
            ["thanks choppa"] = "thanks",
            ["thanks a lot"] = "thanks",
            ["thank you so much"] = "thanks",
            ["thx"] = "thanks",
            ["ty"] = "thanks",
            ["youre welcome"] = "welcome",
            ["no problem"] = "welcome",
            ["my pleasure"] = "welcome",
            ["bye"] = "farewell",
            ["goodbye"] = "farewell",
            ["see you"] = "farewell",
            ["see ya"] = "farewell",
            ["catch you later"] = "farewell",
            ["talk to you later"] = "farewell",
            ["later"] = "farewell",
            ["good night"] = "night",
            ["goodnight"] = "night",
            ["gn"] = "night",
            ["please"] = "polite",
            ["who are you"] = "identity",
            ["what are you"] = "identity",
            ["whats your name"] = "identity",
            ["what can you do"] = "help",
            ["can you help me"] = "help",
            ["help"] = "help",
            ["help me"] = "help",
            ["tell me a joke"] = "joke",
            ["tell a joke"] = "joke",
            ["joke"] = "joke",
            ["another joke"] = "joke",
            ["tell me another joke"] = "joke",
            ["make me laugh"] = "joke",
            ["make me smile"] = "joke",
            ["say something funny"] = "joke",
            ["do you know any jokes"] = "joke",
            ["tell me a dad joke"] = "dad-joke",
            ["tell a dad joke"] = "dad-joke",
            ["dad joke"] = "dad-joke",
            ["dad jokes"] = "dad-joke",
            ["tell me a pun"] = "pun",
            ["tell me a punny joke"] = "pun",
            ["tell a pun"] = "pun",
            ["pun"] = "pun",
            ["puns"] = "pun",
            ["tell me a knock knock joke"] = "knock-knock",
            ["tell a knock knock joke"] = "knock-knock",
            ["knock knock joke"] = "knock-knock",
            ["knock knock"] = "knock-knock",
            ["tell me a tech joke"] = "tech-joke",
            ["tell me a computer joke"] = "tech-joke",
            ["tell a technology joke"] = "tech-joke",
            ["tech joke"] = "tech-joke",
            ["computer joke"] = "tech-joke",
            ["programmer joke"] = "tech-joke",
            ["tell me an animal joke"] = "animal-joke",
            ["tell me a joke about animals"] = "animal-joke",
            ["tell me a dog joke"] = "animal-joke",
            ["tell me a cat joke"] = "animal-joke",
            ["animal joke"] = "animal-joke",
            ["dog joke"] = "animal-joke",
            ["cat joke"] = "animal-joke",
            ["tell me a one liner"] = "one-liner",
            ["tell me a one liner joke"] = "one-liner",
            ["one liner"] = "one-liner",
            ["one liner joke"] = "one-liner",
            ["evil joke"] = "evil-joke",
            ["evil jokes"] = "evil-joke",
            ["tell me an evil joke"] = "evil-joke",
            ["tell me evil jokes"] = "evil-joke",
            ["tell an evil joke"] = "evil-joke",
            ["another evil joke"] = "evil-joke",
            ["ok"] = "acknowledgment",
            ["okay"] = "acknowledgment",
            ["k"] = "acknowledgment",
            ["kk"] = "acknowledgment",
            ["got it"] = "acknowledgment",
            ["gotcha"] = "acknowledgment",
            ["alright"] = "acknowledgment",
            ["sounds good"] = "acknowledgment",
            ["cool"] = "acknowledgment",
            ["nice"] = "acknowledgment",
            ["sweet"] = "acknowledgment",
            ["great"] = "acknowledgment",
            ["awesome"] = "acknowledgment",
            ["bet"] = "acknowledgment",
            ["word"] = "acknowledgment",
            ["fair enough"] = "acknowledgment",
            ["makes sense"] = "acknowledgment",
            ["i see"] = "acknowledgment",
            ["oh ok"] = "acknowledgment",
            ["true"] = "acknowledgment",
            ["facts"] = "acknowledgment",
            ["fr"] = "acknowledgment",
            ["for real"] = "acknowledgment",
            ["yes"] = "affirmative",
            ["yeah"] = "affirmative",
            ["yep"] = "affirmative",
            ["yup"] = "affirmative",
            ["yea"] = "affirmative",
            ["ya"] = "affirmative",
            ["sure"] = "affirmative",
            ["definitely"] = "affirmative",
            ["of course"] = "affirmative",
            ["absolutely"] = "affirmative",
            ["no"] = "negative",
            ["nah"] = "negative",
            ["nope"] = "negative",
            ["nah im good"] = "negative",
            ["not really"] = "negative",
            ["no thanks"] = "negative",
            ["pass"] = "negative",
            ["lol"] = "laughter",
            ["lmao"] = "laughter",
            ["haha"] = "laughter",
            ["hehe"] = "laughter",
            ["rofl"] = "laughter",
            ["funny"] = "laughter",
            ["hahaha"] = "laughter",
            ["hilarious"] = "laughter",
            ["wbu"] = "reciprocal",
            ["hbu"] = "reciprocal",
            ["what about you"] = "reciprocal",
            ["how about you"] = "reciprocal",
            ["and you"] = "reciprocal",
            ["u"] = "reciprocal",
            ["you"] = "reciprocal",
            ["idk"] = "uncertainty",
            ["i dont know"] = "uncertainty",
            ["maybe"] = "uncertainty",
            ["dunno"] = "uncertainty",
            ["not sure"] = "uncertainty",
            ["bruh"] = "reaction",
            ["bro"] = "reaction",
            ["dude"] = "reaction",
            ["damn"] = "reaction",
            ["dang"] = "reaction",
            ["wow"] = "reaction",
            ["woah"] = "reaction",
            ["omg"] = "reaction",
            ["sheesh"] = "reaction",
            ["good"] = "state",
            ["bad"] = "state",
            ["fine"] = "state",
            ["not bad"] = "state"
        };

    public static bool TryRespond(string phrase, out string response, string? displayName = null)
    {
        var normalized = Normalize(phrase);
        if (PhraseGroups.TryGetValue(normalized, out var group)
            || TryMatchJokeCategory(normalized, out group))
        {
            if (Responses.TryGetValue(group, out var options))
            {
                response = options[Random.Shared.Next(options.Length)];
                if (!string.IsNullOrWhiteSpace(displayName)
                    && group is "greeting" or "morning" or "afternoon" or "evening")
                {
                    var punctuationIndex = response.IndexOfAny(['!', '.', '?']);
                    response = punctuationIndex < 0
                        ? $"{response}, {displayName}."
                        : $"{response[..punctuationIndex]}, {displayName}{response[punctuationIndex..]}";
                }

                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    private static bool TryMatchJokeCategory(string normalized, out string group)
    {
        var categoryPrefixes = new (string Prefix, string Group)[]
        {
            ("tell me a ", "joke"),
            ("tell me another ", "joke"),
            ("tell me ", "joke"),
            ("tell a ", "joke"),
            ("give me a ", "joke"),
            ("give me ", "joke"),
            ("make me a ", "joke")
        };

        foreach (var (prefix, defaultGroup) in categoryPrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var request = normalized[prefix.Length..];
            var isJokeRequest =
                request.Contains("joke", StringComparison.Ordinal)
                || request.Contains("funny", StringComparison.Ordinal)
                || request is "a pun" or "another pun" or "some puns"
                    or "a dad joke" or "another dad joke"
                    or "a knock knock" or "a knock knock joke"
                    or "a tech joke" or "a computer joke" or "a programmer joke"
                    or "an animal joke" or "a dog joke" or "a cat joke" or "a fish joke"
                    or "a one liner" or "a one liner joke" or "an evil joke";
            if (!isJokeRequest)
            {
                continue;
            }

            group = request switch
            {
                var text when text.Contains("dad joke", StringComparison.Ordinal) => "dad-joke",
                var text when text.Contains("knock knock", StringComparison.Ordinal) => "knock-knock",
                var text when text.Contains("pun", StringComparison.Ordinal) => "pun",
                var text when text.Contains("tech", StringComparison.Ordinal)
                    || text.Contains("computer", StringComparison.Ordinal)
                    || text.Contains("programmer", StringComparison.Ordinal)
                    || text.Contains("coding", StringComparison.Ordinal) => "tech-joke",
                var text when text.Contains("animal", StringComparison.Ordinal)
                    || text.Contains("dog", StringComparison.Ordinal)
                    || text.Contains("cat", StringComparison.Ordinal)
                    || text.Contains("fish", StringComparison.Ordinal) => "animal-joke",
                var text when text.Contains("evil", StringComparison.Ordinal)
                    || text.Contains("villain", StringComparison.Ordinal) => "evil-joke",
                var text when text.Contains("one liner", StringComparison.Ordinal) => "one-liner",
                var text when text.Contains("joke", StringComparison.Ordinal)
                    || text.Contains("funny", StringComparison.Ordinal) => "joke",
                _ => defaultGroup
            };
            return true;
        }

        group = string.Empty;
        return false;
    }

    private static string Normalize(string phrase)
    {
        var characters = phrase
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return string.Join(' ', new string(characters)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
