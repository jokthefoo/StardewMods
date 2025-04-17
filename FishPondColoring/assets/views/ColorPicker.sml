<lane orientation="vertical"
      horizontal-content-alignment="middle">
    <banner background={@Mods/StardewUI/Sprites/BannerBackground}
            background-border-thickness="48,0"
            padding="12"
            text="Set Pond Color" 
    />
    <frame layout="500px content"
           margin="0,8,0,0"
           padding="32,24"
           background={@Mods/StardewUI/Sprites/ControlBorder}>
        <lane layout="stretch content" orientation="vertical">
            <color-picker color={<>PondColor} />
            <panel layout="128px" vertical-content-alignment="end"
                   margin="170,8,0,0">
                <image layout="128px content"
                       sprite={@Mods/Jok.ColorfulPonds/Sprites/UISprites:FishPondWater}
                tint = {PondColor}/>
                <grid layout="120px 120px"
                      item-layout="length: 25"
                      item-spacing="0,0"
                      margin="10,0,0,5"
                      horizontal-item-alignment="middle">
                    <image *repeat={AllWater}
                    layout="25px 25px"
                    sprite={WaterTile}
                    opacity=".3"/>
                </grid>
                <image layout="128px content"
                       sprite={@Mods/Jok.ColorfulPonds/Sprites/UISprites:FishPond} />
            </panel>
        </lane>
    </frame>
    <lane layout="stretch content"
          margin="16, 8, 0, 0"
          horizontal-content-alignment="end"
          vertical-content-alignment="middle">
        <button margin="0, 0, 118, 0"
                text="Reset Pond Color"
                left-click=|ResetPondColor()|
        hover-background={@Mods/StardewUI/Sprites/ButtonLight}
        />
        <button margin="16, 0, 16, 0"
                text="Cancel"
                hover-background={@Mods/StardewUI/Sprites/ButtonLight}
                left-click=|Close("false")| />
        <button text="Ok"
                hover-background={@Mods/StardewUI/Sprites/ButtonLight}
                left-click=|Close("true")| />
    </lane>
</lane>

<template name="form-heading">
    <label font="dialogue"
           margin="0,0,0,8"
           text={&text}
           shadow-alpha="0.6"
           shadow-layers="VerticalAndDiagonal"
           shadow-offset="-3, 3" />
</template>

<template name="form-row">
    <lane layout="stretch content"
          margin="16,4"
          vertical-content-alignment="middle">
        <label layout="280px content"
               margin="0,8"
               text={&title}
               shadow-alpha="0.8"
               shadow-color="#4448"
               shadow-offset="-2, 2" />
        <outlet />
    </lane>
</template>
