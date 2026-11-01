package local.lunar;

import com.google.protobuf.*;
import com.lunarclient.common.v1.Uuid;
import com.lunarclient.websocket.cosmetic.v2.*;
import com.lunarclient.websocket.badge.v1.EquipBadgeRequest;
import com.lunarclient.websocket.badge.v1.EquipBadgeResponse;
import java.io.IOException;
import java.nio.file.*;
import java.util.*;

/** Local outfit storage. None of the mutation handlers have a network channel. */
public final class LocalCosmetics {
    private static LoginResponse state;
    private static Path file;

    public static void login(RpcChannel channel, RpcController controller, LoginRequest request,
                             final RpcCallback<LoginResponse> callback) {
        channel.callMethod(CosmeticService.getDescriptor().getMethods().get(0), controller, request,
            LoginResponse.getDefaultInstance(), RpcUtil.generalizeCallback(new RpcCallback<LoginResponse>() {
                public void run(LoginResponse server) {
                    callback.run(initialize(server));
                }
            }, LoginResponse.class, LoginResponse.getDefaultInstance()));
    }

    private static synchronized LoginResponse initialize(LoginResponse server) {
        if (server == null || !server.hasOutfitTree() || !server.getOutfitTree().hasDefaultOutfitId())
            throw new IllegalStateException("Local cosmetics require an account with a default outfit.");
        Uuid account = server.getOutfitTree().getDefaultOutfitId();
        Path directory = Paths.get(System.getProperty("lunar.localCosmeticsDir",
            Paths.get(System.getProperty("user.home"), ".lunarclient", "local-cosmetics").toString()));
        file = directory.resolve(Long.toHexString(account.getHigh64()) + "-" +
            Long.toHexString(account.getLow64()) + ".pb");
        try {
            if (Files.exists(file)) {
                state = LoginResponse.parseFrom(Files.readAllBytes(file));
                if (state.getOutfitsCount() == 0 || !state.hasOutfitTree())
                    throw new IOException("Invalid saved local outfits");
            } else {
                UUID id = UUID.randomUUID();
                Uuid uid = Uuid.newBuilder().setHigh64(id.getMostSignificantBits())
                    .setLow64(id.getLeastSignificantBits()).build();
                state = LoginResponse.newBuilder().addOutfits(Outfit.newBuilder().setId(uid)
                    .setName("Local Outfit")).setOutfitTree(OutfitTree.newBuilder()
                    .setDefaultOutfitId(uid)).build();
                save(state);
            }
        } catch (IOException error) {
            throw new IllegalStateException("Cannot read local cosmetics; no remote update was sent.", error);
        }
        return server.toBuilder().setHasAllCosmeticsFlag(true).clearOutfits()
            .addAllOutfits(state.getOutfitsList()).setOutfitTree(state.getOutfitTree()).build();
    }

    private static void save(LoginResponse next) throws IOException {
        if (file == null) throw new IOException("Local cosmetics have not logged in");
        Files.createDirectories(file.getParent());
        Path temporary = Files.createTempFile(file.getParent(), "outfits-", ".tmp");
        try {
            Files.write(temporary, next.toByteArray());
            Files.move(temporary, file, StandardCopyOption.REPLACE_EXISTING);
            state = next;
        } finally {
            Files.deleteIfExists(temporary);
        }
    }

    public static synchronized void createOutfit(CreateOutfitRequest request,
                                                 RpcCallback<CreateOutfitResponse> callback) throws IOException {
        UUID id = UUID.randomUUID();
        Outfit outfit = Outfit.newBuilder().setId(Uuid.newBuilder().setHigh64(id.getMostSignificantBits())
            .setLow64(id.getLeastSignificantBits())).setName(request.getName()).build();
        save(state.toBuilder().addOutfits(outfit).setOutfitTree(state.getOutfitTree().toBuilder()
            .setDefaultOutfitId(outfit.getId())).build());
        callback.run(CreateOutfitResponse.newBuilder().setStatus(CreateOutfitResponse.Status.STATUS_OK)
            .setOutfit(outfit).build());
    }

    public static synchronized void updateOutfit(UpdateOutfitRequest request,
                                                 RpcCallback<UpdateOutfitResponse> callback) throws IOException {
        LoginResponse.Builder next = state.toBuilder();
        boolean found = false;
        for (int i = 0; i < next.getOutfitsCount(); i++) {
            if (next.getOutfits(i).getId().equals(request.getOutfit().getId())) {
                next.setOutfits(i, request.getOutfit()); found = true; break;
            }
        }
        if (!found) throw new IOException("Unknown local outfit");
        save(next.build());
        callback.run(UpdateOutfitResponse.newBuilder().setStatus(UpdateOutfitResponse.Status.STATUS_OK)
            .setOutfit(request.getOutfit()).build());
    }

    public static synchronized void deleteOutfit(DeleteOutfitRequest request,
                                                 RpcCallback<DeleteOutfitResponse> callback) throws IOException {
        if (request.getOutfitId().equals(state.getOutfitTree().getDefaultOutfitId()))
            throw new IOException("Cannot delete the active local outfit");
        LoginResponse.Builder next = state.toBuilder().clearOutfits();
        for (Outfit outfit : state.getOutfitsList())
            if (!outfit.getId().equals(request.getOutfitId())) next.addOutfits(outfit);
        save(next.build());
        callback.run(DeleteOutfitResponse.newBuilder().setStatus(DeleteOutfitResponse.Status.STATUS_OK).build());
    }

    public static synchronized void selectOutfit(SelectOutfitRequest request,
                                                 RpcCallback<SelectOutfitResponse> callback) throws IOException {
        boolean found = false;
        for (Outfit outfit : state.getOutfitsList())
            if (outfit.getId().equals(request.getOutfitTree().getDefaultOutfitId())) found = true;
        if (!found) throw new IOException("Unknown local outfit");
        save(state.toBuilder().setOutfitTree(request.getOutfitTree()).build());
        callback.run(SelectOutfitResponse.newBuilder().setStatus(SelectOutfitResponse.Status.STATUS_OK).build());
    }

    public static void favoriteCosmetic(FavoriteCosmeticRequest request, RpcCallback<FavoriteCosmeticResponse> callback) {
        callback.run(FavoriteCosmeticResponse.getDefaultInstance());
    }
    public static void unfavoriteCosmetic(UnfavoriteCosmeticRequest request, RpcCallback<UnfavoriteCosmeticResponse> callback) {
        callback.run(UnfavoriteCosmeticResponse.getDefaultInstance());
    }
    public static void equipBadge(EquipBadgeRequest request, RpcCallback<EquipBadgeResponse> callback) {
        callback.run(EquipBadgeResponse.getDefaultInstance());
    }
}
