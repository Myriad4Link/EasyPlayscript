interface transition(type: string) : void
interface play(sound: string, volume: decimal) : void
interface on_complete() : void
interface get_name() : string

script load_tooltip[
你好。
这里是……？

啊、您好！请问你是？

@transition("fade_out")
@play("bgm_title", 0.8)

/

这是第二页。
@get_name()，欢迎来到这个世界。

@on_complete()
]

script intro_dialogue[
@transition("fade_in")
从前有座山，山里有座庙。

庙里有个老和尚，在给小和尚讲故事。

@play("sfx_page_turn", 1.0)
/

讲什么故事呢？

@on_complete()
]
