# This is a comment.

# This is a compiler call. The first identifier is the directive type,
# the second identifier is the name. The content inside the square
# bracket is a "script block".
script load_tooltip[
# "aaa \n bbb" is seen as one sentence. For example, the following structure should be parsed as "您好。这里是……？"
你好。
这里是……？

# "aaa \n\n bbb" is seen as two sentences. For example, the following structure should be parsed as "啊、您好\n请问你是？"
啊、您好！

请问你是？

# Consumer calls can also be made from inside the script block. For example:
@transition("fade_out")

# But script blocks cannot be nested in another script block. For example, the following is illegal:
# script something_inside[...] <-- ILLEGAL FOR NESTED BLOCKS!
]
