package com.bajoterra.core.world;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class NavGraphTest {

    @ParameterizedTest
    @ValueSource(strings = {"caverna", "test_arena", "test_open"})
    void everyNodeIsFreeOfSolidObstacles(String mapId) {
        GameMap map = CONTENT.maps().require(mapId);
        NavGraph nav = map.nav();

        assertThat(nav.size()).isPositive();
        for (int i = 0; i < nav.size(); i++) {
            for (Obstacle o : map.obstacleList()) {
                if (o.blocksMovement() && o.height() > World.STEP_HEIGHT) {
                    assertThat(o.containsXZ(nav.nodeX(i), nav.nodeZ(i), 0.5))
                            .as("node %d inside %s", i, o.id()).isFalse();
                }
            }
        }
    }

    @Test
    void followingNextHopsAlwaysReachesTheDestination() {
        NavGraph nav = CONTENT.maps().require("caverna").nav();
        int from = 0;
        int to = nav.size() - 1;

        int steps = 0;
        int current = from;
        while (current != to && steps++ < nav.size()) {
            current = nav.nextNode(current, to);
            assertThat(current).isNotNegative();
        }

        assertThat(current).isEqualTo(to);
        assertThat(nav.pathLength(from, to)).isFinite();
    }

    @Test
    void pathsBendAroundWalls() {
        GameMap map = CONTENT.maps().require("test_arena");
        NavGraph nav = map.nav();
        int west = nav.nearestNode(-10, 0);
        int east = nav.nearestNode(10, 0);

        double straight = Math.hypot(nav.nodeX(east) - nav.nodeX(west), nav.nodeZ(east) - nav.nodeZ(west));

        assertThat(nav.pathLength(west, east)).as("detour around the wall").isGreaterThan((float) straight + 2f);
    }

    @Test
    void distancesAreSymmetric() {
        NavGraph nav = CONTENT.maps().require("caverna").nav();

        for (int i = 0; i < nav.size(); i += 7) {
            for (int j = 0; j < nav.size(); j += 11) {
                assertThat(nav.pathLength(i, j)).isEqualTo(nav.pathLength(j, i));
            }
        }
    }

    @Test
    void nearestNodeIsTheClosestOne() {
        NavGraph nav = CONTENT.maps().require("test_open").nav();
        int node = nav.nearestNode(nav.nodeX(3) + 0.3, nav.nodeZ(3) - 0.2);

        assertThat(node).isEqualTo(3);
    }

    @Test
    void coverPointsHugTallObstaclesAndAreNotInsideSolids() {
        GameMap map = CONTENT.maps().require("caverna");

        assertThat(map.coverCount()).isPositive();
        for (int i = 0; i < map.coverCount(); i++) {
            for (Obstacle o : map.obstacleList()) {
                if (o.blocksMovement() && o.height() > World.STEP_HEIGHT) {
                    assertThat(o.containsXZ(map.coverX(i), map.coverZ(i), 0.0)).isFalse();
                }
            }
            assertThat(map.coverHeight(i)).isGreaterThanOrEqualTo(0.8);
        }
    }
}
